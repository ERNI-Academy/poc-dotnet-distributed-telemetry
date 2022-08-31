using Demo.Service;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
var serviceName = entryAssemblyName?.Name;
var serviceVersion = versionAttribute?.InformationalVersion ?? entryAssemblyName?.Version?.ToString();

var attributes = new Dictionary<string, object>
{
    // See https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions
    ["host.name"] = Environment.MachineName,
    ["os.description"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
};
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName, serviceVersion)
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
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetryTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder.SetResourceBuilder(resourceBuilder)
                         .AddAspNetCoreInstrumentation()
                         .AddEntityFrameworkCoreInstrumentation()
                         .AddNpgsql()
                         .AddOtlpExporter(otlpExporterOptions =>
                         {
                             builder.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(otlpExporterOptions);
                         })
                         .AddConsoleExporter();
});

builder.Services.AddDbContext<WeatherContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("WeatherContext") ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
