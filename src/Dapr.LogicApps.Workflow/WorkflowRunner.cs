using System.Threading.Tasks;
using Microsoft.Azure.Flow.Data.Engines;
using System.Net.Http;
using Microsoft.Azure.Flow.Data.Entities;
using Microsoft.Azure.Flow.Templates.Extensions;
using Microsoft.Azure.Flow.Web.Extensions;
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

namespace Dapr.LogicApps.Workflow
{
    public class DaprWorkflowExecutor : DaprClient.DaprClientBase
    {
        private EdgeFlowConfiguration config { get; set; }
        private string flowName { get; set; }
        const string EventName = "workflowevent";

        public DaprWorkflowExecutor(string name, EdgeFlowConfiguration config)
        {
            this.flowName = name;
            this.config = config;
        }

        public override Task<Empty> OnTopicEvent(CloudEventEnvelope request, Grpc.Core.ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override Task<Any> OnInvoke(InvokeEnvelope request, Grpc.Core.ServerCallContext context)
        {
            Console.WriteLine("Invoking Workflow...");
            
            var any = new Any();

            try
            {
                var response = CallWorkflow();
                any.Value = ByteString.CopyFromUtf8(response);

                return Task.FromResult(any);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException.Message);
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
            var response = CallWorkflow();
            Console.WriteLine(response);

            return Task.FromResult(new BindingResponseEnvelope());
        }

        private string CallWorkflow()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/workflow");
            var flowConfig = this.config;
            var flowName = this.flowName;

            using (RequestCorrelationContext.Current.Initialize((string)null, (string)null, FlowConstants.PrivatePreview20190601ApiVersion, (string)null, (string)null, (string)null, (string)null, (string)null, (string)null, (string)null, (string)null))
            {
                RequestIdentity identity = new RequestIdentity()
                {
                    Claims = new Dictionary<string, string>(),
                    IsAuthenticated = true
                };
                identity.AuthorizeRequest(RequestAuthorizationSource.Direct);
                RequestCorrelationContext.Current.SetAuthenticationIdentity(identity);

                var configHolder = new ConfigHolder();
                configHolder.FlowConfiguration = flowConfig;
                configHolder.HttpConfiguration = new HttpConfiguration();

                var flow = configHolder.GetRegionalDataProvider().FindFlowByName(FlowConfiguration.EdgeSubscriptionId, FlowConfiguration.EdgeResourceGroupName, flowName).Result;
                var triggerName = flow.Definition.Triggers.Single<System.Collections.Generic.KeyValuePair<string, FlowTemplateTrigger>>().Key;

                var token = FlowHttpEngine.GetHttpActionOutput(flowConfig.EventSource, req, CancellationToken.None).Result;

                var trigger = flow.Definition.GetTrigger(triggerName);
                var parameterValues = TemplateEngine.ResolveParameters(flow.Definition, flow.Parameters);
                TemplateEngine.ProcessTemplateLanguageExpressions(flow.Definition, parameterValues, null, null);

                var subscription = configHolder.GetRegionalCacheProvider().FindSubscription(flow.SubscriptionId).Result;
                string flowRunSequenceId = !req.IsCalledByWorkflowAction() || !req.Properties.ContainsKey(FlowConstants.FlowWorkflowRunSequenceIdPropertyName) ? FlowRunSequenceUtility.GenerateNextRunSequenceId((string)null, flow.GetRandomizedRuntimeScaleUnit(configHolder.GetScaleUnitAllocationProvider(), subscription, null)) : req.Properties[FlowConstants.FlowWorkflowRunSequenceIdPropertyName].ToString();
                string[] clientKeywords = GetRequestClientKeywords(req);
                string defaultClientTrackingId = GetDefaultClientTrackingId(req, flowRunSequenceId);

                var responseReadyJobSubscriber = !req.IsCalledByWorkflowAction() || !req.Properties.ContainsKey(FlowConstants.FlowRunResponseReadyJobSubscriberPropertyName) ? (FlowJobNotificationSubscriber)null : req.Properties[FlowConstants.FlowRunResponseReadyJobSubscriberPropertyName].Cast<FlowJobNotificationSubscriber>();
                var helper = TemplateExpressionsHelper.GetTemplateFunctionEvaluationHelper(parameterValues, triggerName, (JToken)null, flow.TemplateRuntimeContext.HasOpenApiOperations(), (FlowRunOperationDependencyTrackingContext)null);
                bool isSchemaValidationNeeded = Microsoft.Azure.Flow.Templates.Extensions.FlowTemplateTriggerExtensions.IsFlowRequestTrigger(trigger) && (trigger.OperationOptions.IsEnableSchemaValidationSet() || flow.TemplateRuntimeContext.HasOpenApiOperations());

                var evaluatedTriggerBodySchema = isSchemaValidationNeeded ? trigger.GetEvaluatedBodySchemaFromInputs(helper.EvaluationContext) : (JToken)null;
                var evaluatedTriggerHeadersSchema = isSchemaValidationNeeded ? trigger.GetEvaluatedHeadersSchemaFromInputs(helper.EvaluationContext) : (JToken)null;
                var triggerResult = CreateFlowOperationResult(flow, triggerName, trigger, token, clientKeywords, parameterValues, helper, evaluatedTriggerBodySchema, evaluatedTriggerHeadersSchema);
                var updatedTemplateRuntimeContext = flow.TemplateRuntimeContext;
                SwaggerSchema swaggerSchema;

                if (!helper.EnablePreserveAnnotations)
                {
                    swaggerSchema = (SwaggerSchema)null;
                }
                else
                {
                    JToken inputs = triggerResult.Inputs;
                    swaggerSchema = inputs != null ? inputs.ExtractFormatFromAnnotations() : (SwaggerSchema)null;
                }
                SwaggerSchema triggerInputsSchema = swaggerSchema;
                var triggerValue = triggerResult.ToJToken();

                triggerResult = configHolder.GetFlowRunEngine(flow.ScaleUnit, null).UploadFlowTriggerResult(flow.FlowId, flowRunSequenceId, triggerResult, true).Result;
                bool synchronousResponseAwaited = true;
                bool nestedWorkflowResponseAwaited = req.IsCalledByWorkflowAction() && req.Properties.ContainsKey(FlowConstants.FlowRunResponseEndTimePropertyName);
                DateTime? responseActionEndTime = nestedWorkflowResponseAwaited ? new DateTime?(Microsoft.WindowsAzure.ResourceStack.Common.Extensions.HttpMessageExtensions.GetProperty<DateTime>(req, FlowConstants.FlowRunResponseEndTimePropertyName)) : (synchronousResponseAwaited ? new DateTime?(DateTime.UtcNow.Add(FlowRunEngine.ResponseWaitTimeout)) : new DateTime?());
                HttpResponseMessage httpResponseMessage;

                using (CancellationTokenSource responseReadyNotificationSource = new CancellationTokenSource())
                {
                    using (configHolder.GetFlowNotificationProvider().SubscribeForRunResponseReadyEvent(flow.FlowId, flowRunSequenceId, (Action<FlowRunResponseReadyMessage>)(notification => responseReadyNotificationSource.Cancel())))
                    {
                        var flowRunEngine = configHolder.GetFlowRunEngine(flow.ScaleUnit, FlowRunSequenceUtility.GetRuntimeScaleUnit(flowRunSequenceId), null);
                        string originFlowRunSequenceId = flowRunSequenceId;
                        string location = flow.Location;
                        var tags = flow.Tags;
                        var sku = flow.Sku;
                        var definition = flow.Definition;
                        InsensitiveDictionary<FlowTemplateParameter> parameters = flow.Parameters;
                        string currentActivityId = RequestCorrelationContext.Current.CurrentActivityId;

                        int num = trigger.RequiresConcurrencyControl() ? 1 : 0;
                        var runStartDelay = new TimeSpan?();
                        var triggerRun = flowRunEngine.ProcessFlowTrigger(subscription, (IFlowIdentifiable)flow, flowRunSequenceId, originFlowRunSequenceId, (string)null, location, tags, sku, definition, parameters, currentActivityId, defaultClientTrackingId, clientKeywords, trigger, triggerName, triggerResult, triggerValue, parameterValues, updatedTemplateRuntimeContext, flow.RuntimeConfiguration, true, num != 0, responseActionEndTime, true, triggerInputsSchema, (SwaggerSchema)null, responseReadyJobSubscriber, (FlowJobNotificationSubscriber)null, (FlowJobNotificationSubscriber)null, runStartDelay).Result;
                        var triggerHistory = FlowTriggerHistory.GetFlowTriggerHistory((IFlowIdentifiable)flow, flowRunSequenceId, triggerName, triggerRun != null, triggerResult, flowRunSequenceId, (string)null, flowRunSequenceId);

                        configHolder.FlowConfiguration.EventSource.LogWorkflowTriggerStart(triggerHistory, flow.Location, flow.ScaleUnit, flow.Tags, trigger.Type, subscription.GetSubscriptionSkuName(), trigger.Kind);
                        configHolder.GetScaleUnitDataProvider(flow.ScaleUnit, null).SaveFlowTriggerHistory(triggerHistory).Wait();
                        configHolder.FlowConfiguration.EventSource.LogWorkflowTriggerEnd(triggerHistory, flow.Location, flow.ScaleUnit, flow.Tags, trigger.Type, subscription.GetSubscriptionSkuName(), trigger.Kind);

                        FlowOperationRuntimeContext triggerOperationRuntimeContext = flow.TemplateRuntimeContext != null ? flow.TemplateRuntimeContext.GetTriggerRuntimeContext(trigger, parameterValues) : (FlowOperationRuntimeContext)null;
                        configHolder.GetFlowBillingUsageProvider().IncrementFlowExecutionCount(subscription, flow.GetFullyQualifiedResourceId(), flow.Tags, trigger.Type.GetFlowUsageType(trigger.Kind, triggerOperationRuntimeContext?.ApiName, subscription), DateTime.UtcNow, 1);
                        configHolder.GetFlowBillingUsageProvider().IncrementStorageConsumption(subscription, flow.GetFullyQualifiedResourceId(), flow.Tags, flow.Sku.GetFlowRetentionThreshold(subscription, flow.RuntimeConfiguration), DateTime.UtcNow, triggerHistory.GetTotalContentSize());
                        if (!(triggerRun?.Response != null & synchronousResponseAwaited))
                            throw new InvalidOperationException("Invalid Operation");

                        HttpResponseMessage response = WaitForSynchronousResponse(configHolder, req, flow, triggerRun.FlowRunSequenceId, CancellationToken.None, responseReadyNotificationSource.Token);
                        httpResponseMessage = response.WithDefaultHeadersForFlowRun(configHolder.FlowConfiguration.RoleLocation, flow.ScaleUnit, flow.FlowName, flow.FlowId, flow.FlowSequenceId, triggerRun);

                        var res = httpResponseMessage.Content.ReadAsStringAsync().Result;

                        Console.WriteLine("");
                        Console.WriteLine("Flow executed. Response: " + res);
                        return res;
                    }
                }
            }
        }

        private static HttpResponseMessage WaitForSynchronousResponse(
           ConfigHolder configHolder,
           HttpRequestMessage request,
           Microsoft.Azure.Flow.Data.Entities.Flow flow,
           string flowRunSequenceId,
           CancellationToken clientCancellationToken,
           CancellationToken responseReadyNotificationToken)
        {
            FlowRun flowRun = configHolder.GetFlowRunEngine(flow.ScaleUnit, (CachedIntegrationServiceEnvironmentRuntime)null).WaitForSynchronousResponseWithTimeout(flow, flowRunSequenceId, clientCancellationToken, responseReadyNotificationToken).Result;
            HttpResponseMessage responseMessage = GetFlowRunResponseMessage(configHolder, request, flow, flowRun, clientCancellationToken, (CachedIntegrationServiceEnvironmentRuntime)null);
            int num;
            if (flowRun.Response.OutputsLink == null)
            {
                FlowStatus? status = flowRun.Status;
                FlowStatus flowStatus = FlowStatus.Running;
                num = !(status.GetValueOrDefault() == flowStatus & status.HasValue) ? 8 : 10;
            }
            else
                num = 4;
            FlowStatus responseStatus = (FlowStatus)num;
            string responseCode = responseMessage.StatusCode != HttpUtility.ClientClosedRequest ? responseMessage.StatusCode.ToString() : ErrorResponseCode.ClientClosedRequest.ToString();
            ErrorResponseMessage responseError = responseMessage.ToErrorResponseMessage();
            configHolder.GetFlowRunEngine(flow.ScaleUnit, (CachedIntegrationServiceEnvironmentRuntime)null).UpdateSynchronousResponseStatus(flowRun, responseStatus, responseCode, responseError).Wait();
            return responseMessage;
        }

        private static HttpResponseMessage GetFlowRunResponseMessage(
              ConfigHolder configHolder,
              HttpRequestMessage request,
              Microsoft.Azure.Flow.Data.Entities.Flow flow,
              FlowRun flowRun,
              CancellationToken clientCancellationToken,
              CachedIntegrationServiceEnvironmentRuntime integrationServiceEnvironmentRuntime = null)
        {
            try
            {
                HttpResponseMessage httpResponseMessage = PrepareRunResponseMessage(configHolder, flow, flowRun, integrationServiceEnvironmentRuntime, clientCancellationToken);
                return httpResponseMessage;
            }
            catch (ErrorResponseMessageException ex)
            {
                HttpResponseMessage responseMessage = request.CreateResponse<ErrorResponseMessage>(ex.HttpStatus, ex.ToErrorResponseMessage(), new HttpConfiguration());
                ex.Headers.CoalesceEnumerable<System.Collections.Generic.KeyValuePair<string, string>>().ForEach<System.Collections.Generic.KeyValuePair<string, string>>((Action<System.Collections.Generic.KeyValuePair<string, string>>)(header => responseMessage.Headers.Add(header.Key, header.Value)));
                return responseMessage;
            }
        }

        private static HttpResponseMessage PrepareRunResponseMessage(
        ConfigHolder configHolder,
        Microsoft.Azure.Flow.Data.Entities.Flow flow,
        FlowRun flowRun,
        CachedIntegrationServiceEnvironmentRuntime integrationServiceEnvironmentRuntime,
        CancellationToken clientCancellationToken)
        {
            JToken responseContent = configHolder.GetFlowRunEngine(flow.ScaleUnit, flowRun.GetRuntimeScaleUnit(flow), null).GetFlowRunResponseContent(flow.FlowId, flowRun, clientCancellationToken).Result;
            ResponseActionInput actionResponse = responseContent.FromJToken<ResponseActionInput>();
            HttpResponseMessage responseMessage = FlowHttpEngine.CreateResponseMessage(actionResponse.StatusCode.ToEnumInsensitively<HttpStatusCode>(), actionResponse.Headers, actionResponse.Body);
            if (responseMessage.StatusCode.IsServerFailureRequest())
                responseMessage.Headers.Add(RequestCorrelationContext.HeaderFailureCause, FailureCause.Trigger);
            return responseMessage;
        }

        private static FlowOperationResult CreateFlowOperationResult(
              Microsoft.Azure.Flow.Data.Entities.Flow flow,
              string triggerName,
              FlowTemplateTrigger trigger,
              JToken triggerOutput,
              string[] clientKeywords,
              InsensitiveDictionary<JToken> parameterValues,
              TemplateExpressionEvaluationHelper helper,
              JToken evaluatedTriggerBodySchema,
              JToken evaluatedTriggerHeadersSchema)
        {
            DateTime utcNow = DateTime.UtcNow;
            return new FlowOperationResult()
            {
                Name = triggerName,
                StartTime = new DateTime?(utcNow),
                EndTime = new DateTime?(utcNow),
                Status = new FlowStatus?(FlowStatus.Succeeded),
                OperationTrackingId = RequestCorrelationContext.Current.CurrentActivityId,
                ClientKeywords = clientKeywords,
                Inputs = trigger.Inputs,
                Outputs = triggerOutput
            };
        }

        private static string[] GetRequestClientKeywords(HttpRequestMessage request)
        {
            return request.Headers.Contains(FlowConstants.ClientKeywordsHeader) ? request.Headers.GetValues(FlowConstants.ClientKeywordsHeader).ToArray<string>() : (string[])null;
        }

        private static string GetDefaultClientTrackingId(HttpRequestMessage request, string flowRunSequenceId)
        {
            return request.Headers.GetFirstOrDefault(FlowConstants.ClientTrackingIdHeader, flowRunSequenceId);
        }
    }

    public class ConfigHolder : IFlowConfigurationHolder
    {
        public FlowConfiguration FlowConfiguration { get; set; }

        public HttpConfiguration HttpConfiguration { get; set; }
    }
}
