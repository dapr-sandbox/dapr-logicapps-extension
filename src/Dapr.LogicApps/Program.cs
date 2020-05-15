// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.LogicApps
{
    using System;
    using System.Linq;
    using System.Threading;
    using Grpc.Core;
    using System.Threading.Tasks;
    using Dapr.LogicApps.Workflow;
    using Dapr.Client.Autogen.Grpc.v1;


    class Program
    {
        const int ServerPort = 50003;
        const string WorkflowsPathArg = "--workflows-path";

        static void Main(string[] args)
        {            
            // Load Workflows
            if (!args.ToList().Any(d=> d == WorkflowsPathArg)) 
            {
                throw new ArgumentNullException($"missing {WorkflowsPathArg} argument");
            }
            var workflowPath = args[args.ToList().IndexOf(WorkflowsPathArg) +1];
            if (string.IsNullOrEmpty(workflowPath))
            {
                throw new Exception($"{WorkflowsPathArg} cannot be empty");
            }

            // Create engine
            var workflowEngine = WorkflowCreator.CreateEngine();

            // Load and register workflows
            var workflows = WorkflowCreator.LoadWorkflows(workflowPath, workflowEngine.Engine);

            // Start and register Dapr gRPC app
            var server = new Server
            {
                Services = { DaprClient.BindService(new DaprWorkflowExecutor(workflows, workflowEngine)) },
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
