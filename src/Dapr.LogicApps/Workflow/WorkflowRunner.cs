// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.LogicApps.Workflow
{
    using System.Threading.Tasks;
    using System.Net.Http;
    using Microsoft.Azure.Workflows.Data.Entities;
    using Microsoft.Azure.Workflows.Templates.Extensions;
    using System.Linq;
    using Microsoft.Azure.Workflows.Data.Configuration;
    using System.Threading;
    using Microsoft.Azure.Workflows.Common.Constants;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Instrumentation;
    using System.Collections.Generic;
    using System;
    using Google.Protobuf.WellKnownTypes;
    using Google.Protobuf;
    using Grpc.Core;
    using Microsoft.Azure.Workflows.Data;
    using Dapr.Client.Autogen.Grpc.v1;


    public class DaprWorkflowExecutor : DaprClient.DaprClientBase
    {
        private List<WorkflowConfig> workflows;
        private WorkflowEngine workflowEngine;

        public DaprWorkflowExecutor(IEnumerable<WorkflowConfig> workflows, WorkflowEngine workflowEngine)
        {
            this.workflows = workflows.ToList();
            this.workflowEngine = workflowEngine;
        }

        public override Task<Empty> OnTopicEvent(CloudEventEnvelope request, Grpc.Core.ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public async override Task<InvokeResponse> OnInvoke(InvokeRequest request, Grpc.Core.ServerCallContext context)
        {
            if (!WorkflowExists(request.Method))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Worflow with name {request.Method} was not found"));

            }
            var resp = await ExecuteWorkflow(request.Method);
            return new InvokeResponse()
            {
                Data = resp,
            };
        }

        private bool WorkflowExists(string name)
        {
            return this.workflows.Any(w => w.Name == name);
        }

        private async Task<Any> ExecuteWorkflow(string name)
        {
            var any = new Any();

            try
            {
                var workflowConfig = this.workflows.First(w => w.Name == name);
                var response = await CallWorkflow(workflowConfig);
                any.Value = ByteString.CopyFromUtf8(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException.Message);
            }
            return any;
        }

        public override Task<GetTopicSubscriptionsEnvelope> GetTopicSubscriptions(Empty request, Grpc.Core.ServerCallContext context)
        {
            return Task.FromResult(new GetTopicSubscriptionsEnvelope());
        }

        public override Task<GetBindingsSubscriptionsEnvelope> GetBindingsSubscriptions(Empty request, Grpc.Core.ServerCallContext context)
        {
            var envelope = new GetBindingsSubscriptionsEnvelope();
            this.workflows.ForEach(w =>
            {
                envelope.Bindings.Add(w.Name);
            });
            return Task.FromResult(envelope);
        }

        public async override Task<BindingResponseEnvelope> OnBindingEvent(BindingEventEnvelope request, Grpc.Core.ServerCallContext context)
        {
            if (!WorkflowExists(request.Name))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Worflow with name {request.Name} was not found"));

            }
            var response = await ExecuteWorkflow(request.Name);
            Console.WriteLine(response.Value.ToStringUtf8());

            return new BindingResponseEnvelope() { Data = response };
        }

        private async Task<Flow> FindExistingFlow(string flowName)
        {
            return await this.workflowEngine.Engine
                .GetRegionalDataProvider()
                .FindFlowByName(subscriptionId: EdgeFlowConfiguration.EdgeSubscriptionId, resourceGroup: EdgeFlowConfiguration.EdgeResourceGroupName, flowName: flowName);
        }

        private async Task<string> CallWorkflow(WorkflowConfig workflow)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/workflow");
            var flowConfig = this.workflowEngine.Config;
            var flowName = workflow.Name;

            flowConfig.FlowEdgeEnvironmentEndpointUri = new Uri("http://localhost");

            using (RequestCorrelationContext.Current.Initialize(apiVersion: FlowConstants.PrivatePreview20190601ApiVersion, localizationLanguage: "en-us"))
            {
                var clientRequestIdentity = new RequestIdentity
                {
                    Claims = new Dictionary<string, string>(),
                    IsAuthenticated = true,
                };
                clientRequestIdentity.AuthorizeRequest(RequestAuthorizationSource.Direct);
                RequestCorrelationContext.Current.SetAuthenticationIdentity(clientRequestIdentity);

                var flow = await FindExistingFlow(workflow.Name);
                var triggerName = flow.Definition.Triggers.Keys.Single();
                var trigger = flow.Definition.GetTrigger(triggerName);

                var ct = CancellationToken.None;

                if (trigger.IsFlowRecurrentTrigger() || trigger.IsNotificationTrigger())
                {
                    await this.workflowEngine.Engine
                        .RunFlowRecurrentTrigger(
                            flow: flow,
                            flowName: flowName,
                            triggerName: triggerName);
                    return "";
                }
                else
                {
                    var triggerOutput = this.workflowEngine.Engine.GetFlowHttpEngine().GetOperationOutput(req, flowConfig.EventSource, ct).Result;
                    var resp = await this.workflowEngine.Engine
                                                .RunFlowPushTrigger(
                                                    request: req,
                                                    context: new FlowDataPlaneContext(flow),
                                                    trigger: trigger,
                                                    subscriptionId: EdgeFlowConfiguration.EdgeSubscriptionId,
                                                    resourceGroup: EdgeFlowConfiguration.EdgeResourceGroupName,
                                                    flowName: flowName,
                                                    triggerName: triggerName,
                                                    triggerOutput: triggerOutput,
                                                    clientCancellationToken: ct);
                    return await resp.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
