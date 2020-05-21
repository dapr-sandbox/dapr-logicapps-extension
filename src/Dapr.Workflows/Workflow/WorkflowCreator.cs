// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Workflows.Workflow
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using Dapr.Workflows.Configuration;
    using Microsoft.Azure.Workflows.Data.Configuration;
    using Microsoft.Azure.Workflows.Data.Definitions;
    using Microsoft.Azure.Workflows.Common.Constants;
    using Microsoft.Azure.Workflows.Common.Extensions;
    using Microsoft.Azure.Workflows.Data.Engines;
    using Microsoft.Azure.Workflows.Web.Engines;
    using Microsoft.Azure.Workflows.Worker;
    using Microsoft.Azure.Workflows.Worker.Dispatcher;
    using Microsoft.WindowsAzure.ResourceStack.Common.Services;
    using Newtonsoft.Json;

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

        public static WorkflowEngine CreateEngine(Credentials credentials)
        {
            Console.WriteLine("Loading Configuration");

            var workflowConfig = new FlowConfigurationManager();
            workflowConfig.SetCredentials(credentials);

            CloudConfigurationManager.Instance = (IConfigurationManager)workflowConfig;

            Console.WriteLine("Creating Edge Configuration");
            var flowConfig = new EdgeFlowConfiguration(CloudConfigurationManager.Instance as Microsoft.WindowsAzure.ResourceStack.Common.Services.AzureConfigurationManager);
            flowConfig.Initialize().Wait();
            flowConfig.EnsureInitialized();

            var httpConfig = new System.Web.Http.HttpConfiguration();
            httpConfig.Formatters = new System.Net.Http.Formatting.MediaTypeFormatterCollection();
            httpConfig.Formatters.Add(FlowJsonExtensions.JsonMediaTypeFormatter);

            var edgeEngine = new EdgeManagementEngine(flowConfig, httpConfig);
            edgeEngine.RegisterEdgeEnvironment().Wait();

            var dispatcher = new EdgeFlowJobsDispatcher(
                flowConfiguration: flowConfig,
                httpConfiguration: httpConfig);

            var callbackFactory = new FlowJobsCallbackFactory(
                flowConfiguration: flowConfig,
                httpConfiguration: httpConfig,
                requestPipeline: null);

            flowConfig.InitializeFlowJobCallbackConfiguration(callbackFactory);

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
