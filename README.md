# About Dotnet Distributed Tracing POC

Example of distributed tracing in .NET, using W3C Trace Context and OpenTelemetry.

## OpenTelemetry Collector example

With many instrumented components, and many destinations, you can quickly get complex many-to-many connections (as can be seen in the complex OpenTelemetry example, with only three components and two destinations).

To address this, and then range of different custom protocols used, OpenTelemetry not only has a standardised way to define traces, metrics, and logs, but also a common OpenTelemetry Protocol, and a Collector service/agent.

This allows a much cleaner instrumentation architecture, with application components forwarding messages to the Collector (possibly using OTLP, although some legacy protocols are also supported), and then the Collector having exporters available for many destination systems.

![Diagram showing components Demo.WebApp, Demo.Service, and Demo.Worker, connecting to a Collector, which then forwards to Jaeger, Loki, and Azure Monitor](./docs/generated/collector-tracing.png)

In the longer term, destination systems have started to support OTLP directly, although it may still be useful to have local agents for batching and augmentation pipelines.

## Requirements

* Dotnet 6.0 SDK, https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu
* Node.JS and npm (Node Package Manager), for node, to run the web front end (`sudo apt install nodejs npm`)
* Docker (with docker compose), for local services, https://docs.docker.com/engine/install/ubuntu/
* Azure subscription, for cloud services
* Powershell, for running scripts, https://docs.microsoft.com/en-us/powershell/scripting/install/install-ubuntu
* Azure CLI, to create cloud resources, https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-linux

If running the existing service (not making changes), make sure you run `npm install` to install dependencies and `az extension add -n application-insights` to add the Azure CLI app insights extension.

For existing dotnet applications, make sure you do `dotnet tool restore` for the needed tools.

## Demonstration

This demonstration follows on from the above, and shows the results of Collector.

To build it yourself from scratch, see below.

Before running the demo, start all the docker dependencies (above) and use the script to create the needed Azure resources.

```powershell
az login
$VerbosePreference = 'Continue'
./deploy-infrastructure.ps1
```

Run the complex demo:

```bash
./start-collector-demo.sh
```

Browse to `https://localhost:44303`, and then Fetch Data to see messages. Check Jaeger at `http://localhost:16686/`.

For Grafana (for Loki logs) browse to `http://localhost:3000/` then select Explore > Loki and enter a query:

```promql
{service_name=~"Demo.*"} | json traceid="traceid"
```

Enable Unique labels, then open a log and select to show only body.

Or a more advanced query (and click select Show all detected fields).

```promql
{service_name=~"Demo.*"} | json traceid="traceid", id=`attributes["Id"]`, name=`attributes["Name"]`, version=`resources["service.version"]`, body="body" | line_format "{{.severity | upper}}: {{.name}}{{if .id}}[{{.id}}] {{end}}{{.body}}" | label_format id="", body=""
```

Check Azure Monitor at `https://portal.azure.com/` opening the log analytics workbench > Logs.

```kusto
union AppDependencies, AppRequests, AppTraces, AppExceptions
| where TimeGenerated  > ago(1h)
| sort by TimeGenerated desc
| project TimeGenerated, OperationId, SeverityLevel, Message, Type, AppRoleName, Properties.SpanId, ParentId
```

Also look at Application Insights for End-to-end tracing > operation > Drill into > sample.

And also Applicatin Insights > Application map.


## Details

### Run local services (RabbitMQ, PostgreSQL, Jaeger, and Loki)

For this example, you also need to run Rabbit MQ, for a service bus, and PostgreSQL, for a database.

You also need to be running local OpenTelemetry collector to send  log messages to, and for instrumentation destinations we also have Jaeger for traces, along with Loki (with MinIO storage and Grafana front end) for logs.

You can start the dependencies via docker compose:

```bash
docker compose -p demo up -d
```

### Jaeger

For more details see https://www.jaegertracing.io/

To check the Jaeger UI, browse to `http://localhost:16686/`


The OpenTelemetry Collector service will also be run in docker, but it will be run separately, after the application is configured.

### RabbitMQ

For more details see https://www.rabbitmq.com/

To check the RabbitMQ console, browse to `http://localhost:15672`

### PostgreSQL and Adminer

For more details see https://www.postgresql.org/

To check the Adminer console, browse to `http://localhost:8080`

### Set up Azure Monitor workbench and Application Insights

Log in to your Azure resources if necessary

```powershell
az login
```

Then use the script to create the required resources, which will also output the required connection string.

```powershell
$VerbosePreference = 'Continue'
./deploy-infrastructure.ps1
```

This will create an Azure Monitor Log Analytics Workspace, and then an Application Insights instance connected to it.

You can log in to the Azure portal to check the logging components were created at `https://portal.azure.com`


### Used Nuget packages

* [MassTransit.RabbitMQ](https://www.nuget.org/packages/MassTransit.RabbitMQ/)

* [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL)
* [Npgsql.OpenTelemetry](https://www.nuget.org/packages/Npgsql.OpenTelemetry)

* [OpenTelemetry.Contrib.Instrumentation.EntityFrameworkCore](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.EntityFrameworkCore)
* [OpenTelemetry.Exporter.Console](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
* [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
* [OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs)
* [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
* [OpenTelemetry.Instrumentation.AspNetCore](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
* [OpenTelemetry.Instrumentation.Http](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)

### Add configuration

Add a configuration section to `appsettings.Development.json`, with the default OTLP port:

```json
  "OpenTelemetry": {
    "OtlpExporter": {
      "Endpoint": "http://localhost:4317/",
      "ExportProcessorType": "Batch",
      "Protocol": "grpc"
    }
  }
```

### OpenTelemetry collector tracing

In `Program.cs`, in the OpenTelemetry configuration, include `AddOtlpExporter()`, binding the options to the configuration section:

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
...

builder.Services.AddOpenTelemetryTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder
        ...
        .AddOtlpExporter(otlpExporterOptions =>
        {
            builder.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
        });
});
```

### OpenTelemetry collector logging

Configure logging with `AddOpenTelemetry()`, and then configure `AddOtlpExporter()`, with the same options section. Also turn on settings to include formatted messages, scopes, and state values.

```csharp
using OpenTelemetry.Logs;
...

builder.Logging
    .AddOpenTelemetry(configure =>
    {
        configure
            .SetResourceBuilder(resourceBuilder)
            .AddOtlpExporter(otlpExporterOptions =>
            {
                builder.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
            });
        configure.IncludeFormattedMessage = true;
        configure.IncludeScopes = true;
        configure.ParseStateValues = true;
    });
```

### Configure the collector service

Before running the collector, you need to create a configuration file, `otel-collector-config.yaml`, with an OTLP receiver and the exporters you need, e.g. Jaeger, Loki, and Azure Monitor.

The settings for Azure Monitor are defined as environment variables, allowing them to be easily passed in.

```yaml
receivers:
  otlp:
    protocols:
      grpc:
      http:

processors:
  batch:

exporters:
  azuremonitor:
    instrumentation_key: "${AZ_INSTRUMENTATION_KEY}"
  jaeger:
    endpoint: jaeger:14250
    tls:
      insecure: true
  logging:
    logLevel: info
  loki:
    endpoint: http://loki:3100/loki/api/v1/push
    format: json
    labels:
      resource:
        deployment.environment: "deployment_environment"
        host.name: "host_name"
        service.name: "service_name"
        service.namespace: "service_namespace"
      record:
        severity: "severity"
    tenant_id: tenant1
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [logging, jaeger, azuremonitor]
    logs:
      receivers: [otlp]
      processors: []
      exporters: [logging, loki, azuremonitor]
```

## Run the example

### Run the OpenTelemetry Collector

You need to pass in the configuration values for Azure Monitor when running the collector container.This uses the Azure CLI, running in PowerShell.

```powershell
$ai = az monitor app-insights component show -a appi-tracedemo-dev -g rg-tracedemo-dev-001 | ConvertFrom-Json
$ai.instrumentationKey
docker run -it --rm `
  -e "AZ_INSTRUMENTATION_KEY=$($ai.instrumentationKey)" `
  --network demo_default `
  -p 4317:4317 `
  -v "$(Join-Path (Get-Location) 'otel-collector-config.yaml'):/etc/otelcol-contrib/config.yaml" `
  otel/opentelemetry-collector-contrib:0.50.0
```

### Run the services

In separate terminals run the service:

```powershell
dotnet run --project Demo.Service --urls "https://*:44301" --environment Development
```

To run the web app front end you need to configure the web API address it will use via an environment variable:

```powershell
$ENV:ASPNETCORE_URLS = "http://localhost:8002"
npm run start --prefix Demo.WebApp/ClientApp
```

Then run the web api in a third terminal:

```powershell
dotnet run --project Demo.WebApp --urls "http://*:8002" --environment Development
```

And run the worker app in a fourth terminal

```powershell
dotnet run --project Demo.Worker --environment Development
```

Generate some activity via the front end at `https://localhost:44303/fetch-data`.

## View Results

### Jaeger traces

To see traces in Jaeger, open the interface at <http://localhost:16686/>, select the service Demo.WebApp and search, which will show you a graph of all traces that service is involved in, along with some key details such as the number of spans and number of errors.

![](images/otel-jaeger-summary.png)

You can click through a specific trace to see the timeline of hierarchical spans, distributed across the services. You can then drill down into specific span attributes.

Note that the asychronous worker does not complete processing the RabbitMQ message until after the primary span is complete.

![](images/otel-jaeger-trace.png)

Jaeger can also so the system architecture, derived from the message relationships between the services.

![](images/otel-jaeger-architecture.png)


### Loki logs (via Grafana)

To see the logs in Loki, open Grafana at <http://localhost:3000/>, and go to the Explore page, then ensure Loki is selected in the drop down.

You can select a stream based on labels, e.g. `service_name`, `deployment_environment`, `host_name`, or `severity` (these are defined in `otel-collector-config.yaml`). You can also use the Log Browser to select the stream values you are interested in.

The selected streams can then be further filtered (e.g. on trace ID), parsed, and formatted.

For example, to select the Demo services and parse the trace ID from the JSON data, use the following query:

```text
{service_name=~"Demo.*"} | json traceid="traceid"
```

Grafana will show a Log Volume graph, which is useful if you are filtering for conditions such as errors.

![Loki, in Grafana, with a graph of events](./images/otel-loki-graph.png)

Below the graph the (raw) log data is shown. For simple scanning, turn on Unique Labels, which will show the other labels, including the parsed traceid, and then open one of the messages and select the "Show this field" icon (an eye) next to the body field.

This cleans up the display and shows key fields (the severity is also indicated in colour on the left), and the log message.

![Loki, in Grafana, showing log messages](./images/otel-loki-logs.png)

The trace IDs can be used to correlate log messages from the same operation.

#### Extracting the message

The LogQL pipeline can be used to manipulate the output, e.g. the following extracts the traceid, body, and other attrbutes from the JSON message, formats the line with several of the fields, then removes those fields so that 'Unique Labels' can be used to view the remaining (e.g. service and trace ID).

```promql
{service_name=~"Demo.*"} | json traceid="traceid", id=`attributes["Id"]`, name=`attributes["Name"]`, version=`resources["service.version"]`, body="body" | line_format "{{.severity | upper}}: {{.name}}{{if .id}}[{{.id}}] {{end}}{{.body}}" | label_format id="", name="", body="", severity=""
```

### View results in Azure Monitor

Log in to Azure Portal, https://portal.azure.com/

#### Logs

Open the Log Analytics workspace that was created. The default will be under
Home > Resource groups > rg-tracedemo-dev-001 > log-tracedemo-dev

Select General > Logs from the left. Dismiss the Queries popup to get to an empty editor.

You can use a query like this to view your logs.

```promql
union AppDependencies, AppRequests, AppTraces, AppExceptions
| where TimeGenerated  > ago(1h)
| sort by TimeGenerated desc
| project TimeGenerated, OperationId, SeverityLevel, Message, Type, AppRoleName, Properties.SpanId, ParentId
```

You will see the related logs have the same OperationId (trace ID).  AppDependencies and AppRequests represent trace parent/child relationships, while AppTraces are the log messages.

![Azure Monitor logs, from OpenTelemetry](./images/otel-azure-logs.png)

#### Performance

Open the Application Insights that was created. The default will be under
Home > Resource groups > rg-tracedemo-dev-001 > appi-tracedemo-dev

Select Investigate > Performance in the left hand menu, then Operation Name "GET WeatherForecast/Get" (the top level operation requesting the page). The right hand side will show the instances.

Click on "Drill into... N Samples" in the bottom right, then select the recent operation.

The page will show the End-to-end transaction with the correlation Operation ID (the same as the console), along with a hierarchical timeline of events.

There will be a top level event for **localhost:44302** with two children for the **Message** and **localhost:44301** (the back end service).

The "View all telemetry" button will show all the messages, including traces.

![Azure Monitor App Insights tracing, from OpenTelemetry](./images/otel-azure-tracing.png)

#### Application Map

Select Investigate > Application Map in the left hand menu for a picture of how your services collaborate, showing how components are related by messages.

For this simple application, the Hierarchical View clearly shows how the WebApp calls the Service, and also sends a message to the Worker.

![Azure Monitor App Insights application map, from OpenTelemetry](./images/otel-azure-map.png)

## Contributing

Please see our [Contribution Guide](CONTRIBUTING.md) to learn how to contribute.

## License

![MIT](https://img.shields.io/badge/License-MIT-blue.svg)

(LICENSE) © {{Year}} [ERNI - Swiss Software Engineering](https://www.betterask.erni)

## Code of conduct

Please see our [Code of Conduct](CODE_OF_CONDUCT.md)

## Stats

Check [https://repobeats.axiom.co/](https://repobeats.axiom.co/) for the right URL

## Follow us

[![Twitter Follow](https://img.shields.io/twitter/follow/ERNI?style=social)](https://www.twitter.com/ERNI)
[![Twitch Status](https://img.shields.io/twitch/status/erni_academy?label=Twitch%20Erni%20Academy&style=social)](https://www.twitch.tv/erni_academy)
[![YouTube Channel Views](https://img.shields.io/youtube/channel/views/UCkdDcxjml85-Ydn7Dc577WQ?label=Youtube%20Erni%20Academy&style=social)](https://www.youtube.com/channel/UCkdDcxjml85-Ydn7Dc577WQ)
[![Linkedin](https://img.shields.io/badge/linkedin-35k-green?style=social&logo=Linkedin)](https://www.linkedin.com/company/erni)

## Contact

📧 [esp-services@betterask.erni](mailto:esp-services@betterask.erni)

## Contributors ✨

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- ALL-CONTRIBUTORS-LIST:END -->
This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!
