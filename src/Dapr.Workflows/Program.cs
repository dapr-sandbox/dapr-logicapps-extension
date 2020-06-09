// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Workflows
{
    using System;
    using System.Linq;
    using System.Threading;
    using Grpc.Core;
    using System.Threading.Tasks;
    using Dapr.Workflows.Workflow;
    using AppCallback.Autogen.Grpc.v1;

    class Program
    {
        const int ServerPort = 50003;
        const string WorkflowsPathArg = "--workflows-path";
        const string StorageAccountKey = "STORAGE_ACCOUNT_KEY";
        const string StorageAccountName = "STORAGE_ACCOUNT_NAME";

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

            // Get Azure Storage credentials
            var envVars = Environment.GetEnvironmentVariables();
            if (!envVars.Contains(StorageAccountKey))
            {
                throw new Exception($"{StorageAccountKey} environment variable not set");
            }

            if (!envVars.Contains(StorageAccountName))
            {
                throw new Exception($"{StorageAccountName} environment variable not set");
            }

            var storageAccountName = envVars[StorageAccountName].ToString();
            var storageAccountKey = envVars[StorageAccountKey].ToString();

            if (string.IsNullOrEmpty(storageAccountName))
            {
                throw new Exception($"{StorageAccountName} environment variablec cannot be empty");
            }

            if (string.IsNullOrEmpty(storageAccountKey))
            {
                throw new Exception($"{StorageAccountKey} environment variablec cannot be empty");
            }

            // Create engine
            var credentials = new Credentials(storageAccountName, storageAccountKey);
            var workflowEngine = WorkflowCreator.CreateEngine(credentials);

            // Load and register workflows
            var workflows = WorkflowCreator.LoadWorkflows(workflowPath, workflowEngine.Engine);
    
            // Start and register Dapr gRPC app
            var server = new Server
            {
                Services = { AppCallback.BindService(new DaprWorkflowExecutor(workflows, workflowEngine)) },
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
