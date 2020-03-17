using System;
using Microsoft.Azure.Flow.Data.Configuration;
using Microsoft.Azure.Flow.Worker;
using Microsoft.Azure.Flow.Data.Extensions;
using Microsoft.Azure.Flow.Worker.Dispatcher;
using Microsoft.WindowsAzure.ResourceStack.Common.Services;
using System.Threading;
using Microsoft.Azure.Flow.Data.Definitions;
using Microsoft.Azure.Flow.Common.Constants;
using System.IO;
using Newtonsoft.Json;
using Daprclient;
using Grpc.Core;
using System.Threading.Tasks;

namespace Microsoft.Dapr.LogicApps.ExecutionEnvironment
{
    class Program
    {
        const int ServerPort = 50003;
        const string DefaultFlowName = "ResponseLogicApp";

        static void Main(string[] args)
        {
            var flowName = Environment.GetEnvironmentVariable("FLOW_NAME");
            if (string.IsNullOrEmpty(flowName)) {
                flowName = DefaultFlowName;
            }

            // Create Workflow
            var flowConfig = CreateWorkflow(flowName);

            // Start and register Dapr gRPC app
            var server = new Server
            {
                Services = { DaprClient.BindService(new DaprWorkflowExecutor(flowName, flowConfig)) },
                Ports = { new ServerPort("localhost", ServerPort, ServerCredentials.Insecure) }
            };

            Task.Run(() =>
            {
                server.Start();
            });

            Console.WriteLine("Dapr LogicApps Server listening on port " + ServerPort);
            System.Threading.Thread.Sleep(Timeout.Infinite);
            server.ShutdownAsync().Wait();
        }

        private static EdgeFlowConfiguration CreateWorkflow(string name)
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
