using MassTransit;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry service resource details
var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
var entryAssemblyName = entryAssembly?.GetName();
var versionAttribute = entryAssembly?.GetCustomAttributes(false)
                                     .OfType<AssemblyInformationalVersionAttribute>()
                                     .FirstOrDefault();
var attributes = new Dictionary<string, object>
{
    // See https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions
    ["host.name"] = Environment.MachineName,
    ["os.description"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
};
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(entryAssemblyName?.Name, serviceVersion: versionAttribute?.InformationalVersion ?? entryAssemblyName?.Version?.ToString())
    .AddTelemetrySdk()
    .AddAttributes(attributes);

// Configure logging
builder.Logging.ClearProviders()
               .AddOpenTelemetry(configure =>
               {
                   configure.IncludeFormattedMessage = true;
                   configure.IncludeScopes = true;
                   configure.ParseStateValues = true;
                   configure.SetResourceBuilder(resourceBuilder)
                            .AddOtlpExporter(otlpExporterOptions =>
                            {
                                builder.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
                            })
                            .AddConsoleExporter();
               });


// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddOpenTelemetryTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder.SetResourceBuilder(resourceBuilder)
                         .AddAspNetCoreInstrumentation()
                         .AddHttpClientInstrumentation()
                         .AddSource("MassTransit")
                         .AddOtlpExporter(otlpExporterOptions =>
                         {
                             builder.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
                         })
                         .AddConsoleExporter();
});

builder.Services.AddMassTransit(mtConfig =>
{
    mtConfig.UsingRabbitMq((context, rabbitConfig) =>
    {
        rabbitConfig.Host(builder.Configuration.GetValue<string>("MassTransit:RabbitMq:Host"),
                          builder.Configuration.GetValue<ushort>("MassTransit:RabbitMq:Port"),
                          builder.Configuration.GetValue<string>("MassTransit:RabbitMq:VirtualHost"),
          hostConfig =>
          {
              hostConfig.Username(builder.Configuration.GetValue<string>("MassTransit:RabbitMq:Username"));
              hostConfig.Password(builder.Configuration.GetValue<string>("MassTransit:RabbitMq:Password"));
          }
        );
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();
