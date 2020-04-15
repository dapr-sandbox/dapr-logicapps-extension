using System;
using System.IO;
using Dapr.LogicApps.Configuration;
using Microsoft.Azure.Flow.Data.Configuration;
using Microsoft.Azure.Flow.Data.Definitions;
using Microsoft.Azure.Flow.Worker;
using Microsoft.Azure.Flow.Worker.Dispatcher;
using Microsoft.Azure.Flow.Data.Extensions;
using Microsoft.WindowsAzure.ResourceStack.Common.Services;
using Newtonsoft.Json;
using Microsoft.Azure.Flow.Common.Constants;
using System.Threading;

namespace Dapr.LogicApps.Workflow
{
    public static class WorkflowCreator
    {
        public static EdgeFlowConfiguration Create(string name)
        {
            Console.WriteLine("Starting Dapr Logic Apps Execution Environment");
            Console.WriteLine("Loading Configuration");
            CloudConfigurationManager.Instance = (IConfigurationManager)new FlowConfigurationManager();

            Console.WriteLine("Creating Edge Configuration");
            var flowConfig = new EdgeFlowConfiguration();
            flowConfig.Initialize().Wait();

            Console.WriteLine("Registering Edge Environment");
            var dispatcher = (FlowJobsDispatcher)new EdgeFlowJobsDispatcher(flowConfig, new System.Web.Http.HttpConfiguration(), null);
            var engine = dispatcher.GetEdgeManagementEngine();
            engine.RegisterEdgeEnvironment().Wait();

            var filename = "./ResponseLogicApp.json";
            var path = Environment.GetEnvironmentVariable("WORKFLOW_DIR_PATH");
            if (!string.IsNullOrEmpty(path))
            {
                filename = Path.Join(path, filename);
            }

            Console.WriteLine("Loading definition from " + filename);
            var workflowJson = File.ReadAllText(filename);
            var workflowDef = JsonConvert.DeserializeObject<FlowPropertiesDefinition>(workflowJson);
            var def = new FlowDefinition(FlowConstants.GeneralAvailabilitySchemaVersion);
            def.Properties = workflowDef;

            var response = engine.CreateFlow(name, def, CancellationToken.None).Result;
            Console.WriteLine("Flow Created: " + response.Id);

            return flowConfig;
        }
    }
}
