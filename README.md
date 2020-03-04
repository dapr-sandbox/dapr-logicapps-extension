# Icarus - A Dapr-enabled runtime host for Azure Logic Apps

![Icarus](./assets/icarus.png)

Icarus is a lightweight host for Azure Logic Apps written in dotnet core that allows developers to execute Azure Logic Apps workflows using Dapr.

## How it works

Icarus starts a gRPC server that implements the Dapr Client API and registers for Dapr binding triggers, as well as receiving response-requests style invocations over HTTP or gRPC.

Once a workflow request comes in, Icarus uses the Azure Logic Apps DLLs to execute the workflow in-proc.

## Quick start

Prerequisites:

1. Install [Dapr CLI](https://github.com/dapr/cli#getting-started)

### Kubernetes

Make sure you have a running Kubernetes cluster and `kubectl` in your path.

#### Deploy Dapr

Once you have the Dapr CLI installed, run:

```
dapr init --kubernetes
```

Wait until the Dapr pods have the status `Running`.

#### Deploy Icarus

```
kubectl apply -f deploy/deploy.yaml
```

#### Invoke Logic Apps using Dapr

Create a port-forward to the logic apps container:

```
kubectl port-forward deploy/dapr-logicapps-host 3500:3500
```

Now, invoke logic apps through Dapr:

```
curl http://localhost:3500/v1.0/invoke/logicapps/method/workflow

{"value":"Hello from LogicApp running in Dapr runtime"}                                                                                   
```

Rejoice!

### Self hosted

#### Deploy Dapr

Once you have the Dapr CLI installed, run:

```
dapr init
```

#### Invoke Logic Apps using Dapr

```
dapr run --app-id logicapps --protocol grpc --app-port 50003  dotnet run

{"value":"Hello from LogicApp running in Dapr runtime"}                                                                                   
```

Rejoice once more!

### Invoking Logic Apps using Dapr bindings

First, create any Dapr binding of your choice.
See [this](https://github.com/dapr/docs/tree/master/howto/trigger-app-with-input-binding) How-To tutorial and [sample](https://github.com/dapr/samples/tree/master/5.bindings) to get started.

#### Kubernetes

```
kubectl apply -f my_binding.yaml
```

#### Self hosted

Place the binding yaml file in a `components` in the top level of your app dir.

#### Seeing events triggering logic apps

Once an event is sent to the bindings components, check the logs of Icarus to see the output.

In standalone mode, the output will be printed to the local terminal.

On Kubernetes, run the following command:

```
kubectl logs -l app=dapr-logicapps-host -c host
```

## Build

Make sure you have dotnet core installed on your machine.

1. Clone the repo
2. Inside the top level dir, run: `dotnet build`

### Build Docker Image

Make sure you have Docker installed on your machine.

Compile to release mode:

```
dotnet publish -c Release 
```

Build image:

```
docker build -t <registry>/<image> .
```

Push image:

```
docker push <registry>/<image>
```

## To Do

* Stateful workflows
* Custom connectors
* Support multiple bindings
* async/await-isms
* Remove unused dlls in /bin