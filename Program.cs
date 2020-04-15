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
using Dapr.LogicApps.Workflow;
using Dapr.LogicApps.Configuration;

namespace Dapr.LogicApps
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
            var flowConfig = WorkflowCreator.Create(flowName);

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
    }
}
