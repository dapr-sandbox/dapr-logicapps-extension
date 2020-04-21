using System;
using System.IO;
using System.Collections.Generic;
using Dapr.LogicApps.Configuration;
using Microsoft.Azure.Flow.Data.Configuration;
using Microsoft.Azure.Flow.Data.Definitions;
using Microsoft.Azure.Flow.Worker;
using Microsoft.WindowsAzure.ResourceStack.Common.Services;
using Newtonsoft.Json;
using Microsoft.Azure.Flow.Common.Constants;
using Microsoft.Azure.Flow.Data.Engines;
using Microsoft.Azure.Flow.Web.Engines;
using Microsoft.Azure.Flow.Common.Extensions;

namespace Dapr.LogicApps.Workflow
{
    public class WorkflowEngine
    {
        public EdgeFlowConfiguration Config { get; set; }
        public EdgeFlowWebManagementEngine Engine { get; set; }
    }

    public class WorkflowConfig
    {
        public string Name { get; set; }
        public FlowDefinition Definition { get; set; }
        public WorkflowConfig(string name, FlowDefinition definition)
        {
            this.Name = name;
            this.Definition = definition;
        }
    }

    public static class WorkflowCreator
    {
        public static IEnumerable<WorkflowConfig> LoadWorkflows(string workflowsDir, EdgeFlowWebManagementEngine engine)
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
                engine.ValidateAndCreateFlow(flowName, def.Properties).Wait();
                Console.WriteLine("Flow Created");
                yield return new WorkflowConfig(flowName, def);
            }
        }

        public static WorkflowEngine CreateEngine()
        {
            Console.WriteLine("Loading Configuration");
            CloudConfigurationManager.Instance = (IConfigurationManager)new FlowConfigurationManager();

            Console.WriteLine("Creating Edge Configuration");
            var flowConfig = new EdgeFlowConfiguration();
            flowConfig.Initialize().Wait();
            flowConfig.EnsureInitialized();

            var httpConfig = new System.Web.Http.HttpConfiguration();
            httpConfig.Formatters = new System.Net.Http.Formatting.MediaTypeFormatterCollection();
            httpConfig.Formatters.Add(FlowJsonExtensions.JsonMediaTypeFormatter);

            var edgeEngine = new EdgeManagementEngine(flowConfig, httpConfig);
            edgeEngine.RegisterEdgeEnvironment().Wait();

            var dispatcher = new EdgeFlowJobsDispatcher(
                flowConfiguration: flowConfig,
                httpConfiguration: httpConfig,
                requestPipeline: null);

            dispatcher.Start();
            dispatcher.ProvisionSystemJobs();

            Console.WriteLine("Registering Web Environment");
            var engine = new EdgeFlowWebManagementEngine(flowConfig, httpConfig);

            return new WorkflowEngine()
            {
                Engine = engine,
                Config = flowConfig
            };
        }
    }
}
