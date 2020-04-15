using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Azure.Flow.Data.Engines;

namespace Dapr.LogicApps.Workflow
{
    public class WorkflowEngine
    {
        public EdgeFlowConfiguration Config { get; set; }
        public EdgeManagementEngine Engine { get; set; }
    }

    public class WorkflowConfig
    {
        public string Name { get; set; }
        public WorkflowConfig(string name)
        {
            this.Name = name;
        }
    }

    public static class WorkflowCreator
    {
        public static IEnumerable<WorkflowConfig> LoadWorkflows(string workflowsDir, EdgeManagementEngine engine)
        {
            if (!Directory.Exists(workflowsDir))
            {
                throw new DirectoryNotFoundException($"Couldn't find workflow directory {workflowsDir}");
            }

            foreach (var file in Directory.EnumerateFiles(workflowsDir))
            {
                var fi = new FileInfo(file);
                Console.WriteLine($"Loading workflow: {fi.Name}");

                var workflowJson = File.ReadAllText(fi.FullName);
                var workflowDef = JsonConvert.DeserializeObject<FlowPropertiesDefinition>(workflowJson);
                var def = new FlowDefinition(FlowConstants.GeneralAvailabilitySchemaVersion);
                def.Properties = workflowDef;

                var flowName = Path.GetFileNameWithoutExtension(fi.FullName);
                var response = engine.CreateFlow(flowName, def, CancellationToken.None).Result;
                Console.WriteLine("Flow Created: " + response.Id);

                yield return new WorkflowConfig(flowName);
            }
        }

        public static WorkflowEngine CreateEngine()
        {
            Console.WriteLine("Loading Configuration");
            CloudConfigurationManager.Instance = (IConfigurationManager)new FlowConfigurationManager();

            Console.WriteLine("Creating Edge Configuration");
            var flowConfig = new EdgeFlowConfiguration();
            flowConfig.Initialize().Wait();

            Console.WriteLine("Registering Edge Environment");
            var dispatcher = (FlowJobsDispatcher)new EdgeFlowJobsDispatcher(flowConfig, new System.Web.Http.HttpConfiguration(), null);
            var engine = dispatcher.GetEdgeManagementEngine();
            engine.RegisterEdgeEnvironment().Wait();

            return new WorkflowEngine()
            {
                Engine = engine,
                Config = flowConfig
            };
        }
    }
}
