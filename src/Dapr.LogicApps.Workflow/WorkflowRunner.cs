using System.Threading.Tasks;
using Microsoft.Azure.Flow.Data.Engines;
using System.Net.Http;
using Microsoft.Azure.Flow.Data.Entities;
using Microsoft.Azure.Flow.Templates.Extensions;
using System.Web.Http;
using System.Linq;
using Microsoft.Azure.Flow.Data.Configuration;
using Microsoft.Azure.Flow.Data.Extensions;
using System.Threading;
using Microsoft.Azure.Flow.Common.Constants;
using Microsoft.Azure.Flow.Templates.Schema;
using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
using Microsoft.Azure.Flow.Templates.Engines;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Flow.Data.Utilities;
using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
using Microsoft.Azure.Flow.Data.Events;
using Microsoft.Azure.Flow.Templates.Helpers;
using Microsoft.Azure.Flow.Templates.Expressions;
using Microsoft.Azure.Flow.Templates.Entities;
using Microsoft.Azure.Flow.Common.Entities;
using Microsoft.WindowsAzure.ResourceStack.Common.Instrumentation;
using Microsoft.WindowsAzure.ResourceStack.Common.Swagger.Entities;
using Microsoft.WindowsAzure.ResourceStack.Common.Json;
using Microsoft.Azure.Flow.Common.ErrorResponses;
using Microsoft.Azure.Flow.Data.Operations;
using Microsoft.Azure.Flow.Data.Common.Constants;
using System.Net;
using Microsoft.WindowsAzure.ResourceStack.Common.Utilities;
using System.Collections.Generic;
using Daprclient;
using System;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using Grpc.Core;
using System.Diagnostics;
using Microsoft.Azure.Flow.Data;

namespace Dapr.LogicApps.Workflow
{
    public class DaprWorkflowExecutor : DaprClient.DaprClientBase
    {
        private List<WorkflowConfig> workflows;
        private WorkflowEngine workflowEngine;
        const string EventName = "workflowevent";

        public DaprWorkflowExecutor(IEnumerable<WorkflowConfig> workflows, WorkflowEngine workflowEngine)
        {
            this.workflows = workflows.ToList();
            this.workflowEngine = workflowEngine;
        }

        public override Task<Empty> OnTopicEvent(CloudEventEnvelope request, Grpc.Core.ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override Task<Any> OnInvoke(InvokeEnvelope request, Grpc.Core.ServerCallContext context)
        {
            Console.WriteLine("Invoking Workflow...");
            if (!this.workflows.Any(w => w.Name == request.Method))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Worflow with name {request.Method} was not found"));
            }

            var any = new Any();

            try
            {
                var workflowConfig = this.workflows.First(w => w.Name == request.Method);
                var response = CallWorkflow(workflowConfig);
                any.Value = ByteString.CopyFromUtf8(response);

                return Task.FromResult(any);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException.Message);
                Console.WriteLine(ex.InnerException.StackTrace);
                return Task.FromResult(any);
            }
        }

        public override Task<GetTopicSubscriptionsEnvelope> GetTopicSubscriptions(Empty request, Grpc.Core.ServerCallContext context)
        {
            return Task.FromResult(new GetTopicSubscriptionsEnvelope());
        }

        public override Task<GetBindingsSubscriptionsEnvelope> GetBindingsSubscriptions(Empty request, Grpc.Core.ServerCallContext context)
        {
            var envelope = new GetBindingsSubscriptionsEnvelope();
            envelope.Bindings.Add(EventName);
            return Task.FromResult(envelope);
        }

        public override Task<BindingResponseEnvelope> OnBindingEvent(BindingEventEnvelope request, Grpc.Core.ServerCallContext context)
        {
            var response = CallWorkflow(null);
            Console.WriteLine(response);

            return Task.FromResult(new BindingResponseEnvelope());
        }

        private Task<Flow> FindExistingFlow(string flowName)
        {
            return this.workflowEngine.Engine
                .GetRegionalDataProvider()
                .FindFlowByName(subscriptionId: EdgeFlowConfiguration.EdgeSubscriptionId, resourceGroup: EdgeFlowConfiguration.EdgeResourceGroupName, flowName: flowName);
        }

        private string CallWorkflow(WorkflowConfig workflow)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/workflow");
            var flowConfig = this.workflowEngine.Config;
            var flowName = workflow.Name;

            flowConfig.FlowEdgeEnvironmentEndpointUri = new Uri("http://localhost");

            using (RequestCorrelationContext.Current.Initialize(apiVersion: FlowConstants.PrivatePreview20190601ApiVersion))
            {
                var flow = FindExistingFlow(workflow.Name).Result;
                var triggerName = flow.Definition.Triggers.Keys.Single();
                var trigger = flow.Definition.GetTrigger(triggerName);

                var ct = CancellationToken.None;

                if (trigger.IsFlowRecurrentTrigger() || trigger.IsNotificationTrigger())
                {
                    this.workflowEngine.Engine
                        .RunFlowRecurrentTrigger(
                            flow: flow,
                            flowName: flowName,
                            triggerName: triggerName).Wait();
                    return "";
                }
                else
                {
                    var triggerOutput = this.workflowEngine.Engine.GetFlowHttpEngine().GetOperationOutput(req, flowConfig.EventSource, ct).Result;
                    var resp = this.workflowEngine.Engine
                                                .RunFlowPushTrigger(
                                                    request: req,
                                                    context: new FlowDataPlaneContext(flow),
                                                    trigger: trigger,
                                                    subscriptionId: EdgeFlowConfiguration.EdgeSubscriptionId,
                                                    resourceGroup: EdgeFlowConfiguration.EdgeResourceGroupName,
                                                    flowName: flowName,
                                                    triggerName: triggerName,
                                                    triggerOutput: triggerOutput,
                                                    clientCancellationToken: ct)
                                                .Result;
                    Console.WriteLine(resp.StatusCode);
                    Trace.Flush();
                    return resp.Content.ReadAsStringAsync().Result;
                }
            }
        }
    }

    public class ConfigHolder : IFlowConfigurationHolder
    {
        public FlowConfiguration FlowConfiguration { get; set; }

        public HttpConfiguration HttpConfiguration { get; set; }
    }
}
