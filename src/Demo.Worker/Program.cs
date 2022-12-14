using Demo.Worker;
using MassTransit;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;
using System.Runtime.InteropServices;

static ResourceBuilder GetResourceBuilder(IHostEnvironment hostEnvironment)
{
    // Configure OpenTelemetry service resource details
    // See https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions
    var entryAssembly = Assembly.GetEntryAssembly();
    var entryAssemblyName = entryAssembly?.GetName();
    var versionAttribute = entryAssembly?.GetCustomAttributes(false)
                                         .OfType<AssemblyInformationalVersionAttribute>()
                                         .FirstOrDefault();
    var serviceName = entryAssemblyName?.Name;
    var serviceVersion = versionAttribute?.InformationalVersion ?? entryAssemblyName?.Version?.ToString();
    var attributes = new Dictionary<string, object>
    {
        ["host.name"] = Environment.MachineName,
        ["os.description"] = RuntimeInformation.OSDescription,
        ["deployment.environment"] = hostEnvironment.EnvironmentName.ToLowerInvariant()
    };
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(serviceName, serviceVersion: serviceVersion)
        .AddTelemetrySdk()
        .AddAttributes(attributes);
    return resourceBuilder;
}


IHost host = Host.CreateDefaultBuilder(args)
                  .ConfigureLogging((hostBuilderContext, loggingBuilder) =>
                  {
                      var resourceBuilder = GetResourceBuilder(hostBuilderContext.HostingEnvironment);
                      loggingBuilder.AddOpenTelemetry(configure =>
                      {
                          configure.IncludeFormattedMessage = true;
                          configure.IncludeScopes = true;
                          configure.ParseStateValues = true;
                          configure.SetResourceBuilder(resourceBuilder)
                                   .AddOtlpExporter(otlpExporterOptions =>
                                   {
                                       hostBuilderContext.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
                                   });
                      });
                  })
                 .ConfigureServices((hostBuilderContext, services) =>
                 {
                     var resourceBuilder = GetResourceBuilder(hostBuilderContext.HostingEnvironment);
                     DemoActivitySource.Instance = new System.Diagnostics.ActivitySource("Demo.Worker");
                     services.AddOpenTelemetryTracing(tracerProviderBuilder =>
                     {
                         tracerProviderBuilder.SetResourceBuilder(resourceBuilder)
                                              .AddSource("MassTransit")
                                              .AddOtlpExporter(otlpExporterOptions =>
                                              {
                                                  hostBuilderContext.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
                                              })
                                              .AddConsoleExporter();
                     });
                     services.AddMassTransit(mtConfig =>
                     {
                         mtConfig.AddConsumer<WeatherMessageConsumer>();
                         mtConfig.UsingRabbitMq((context, rabbitConfig) =>
                         {
                             rabbitConfig.Host(hostBuilderContext.Configuration.GetValue<string>("MassTransit:RabbitMq:Host"),
                                 hostBuilderContext.Configuration.GetValue<ushort>("MassTransit:RabbitMq:Port"),
                                 hostBuilderContext.Configuration.GetValue<string>("MassTransit:RabbitMq:VirtualHost"),
                                 hostConfig =>
                                 {
                                     hostConfig.Username(hostBuilderContext.Configuration.GetValue<string>("MassTransit:RabbitMq:Username"));
                                     hostConfig.Password(hostBuilderContext.Configuration.GetValue<string>("MassTransit:RabbitMq:Password"));
                                 }
                             );
                             rabbitConfig.ConfigureEndpoints(context);
                         });
                     });
                 })
                 .Build();

await host.RunAsync();