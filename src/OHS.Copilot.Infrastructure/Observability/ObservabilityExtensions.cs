using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;

namespace OHS.Copilot.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        
        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(TelemetryConstants.ServiceName, TelemetryConstants.ServiceVersion)
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("service.instance.id", Environment.MachineName),
                        new KeyValuePair<string, object>("deployment.environment", settings.DemoMode ? "demo" : "production"),
                        new KeyValuePair<string, object>("service.namespace", "ohs.copilot"),
                        new KeyValuePair<string, object>("service.version", TelemetryConstants.ServiceVersion)
                    });
            })
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new TraceIdRatioBasedSampler(settings.Telemetry?.TraceSamplingRatio ?? 1.0))
                    .AddSource(TelemetryConstants.ActivitySources.AgentPipeline)
                    .AddSource(TelemetryConstants.ActivitySources.VectorStore)
                    .AddSource(TelemetryConstants.ActivitySources.LlmOperations)
                    .AddSource(TelemetryConstants.ActivitySources.DocumentProcessing)
                    .AddSource(TelemetryConstants.ActivitySources.Memory)
                    .AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(TelemetryConstants.ServiceName)
                    .AddConsoleExporter();
            });

        return services;
    }
}

public static class TelemetryConfigurationExtensions
{
    public static void AddTelemetrySettings(this AppSettings settings)
    {
        settings.Telemetry ??= new TelemetrySettings();
        settings.Prometheus ??= new PrometheusSettings();
        settings.Jaeger ??= new JaegerSettings();
    }
}

public class TelemetrySettings
{
    public bool Enabled { get; set; } = true;
    public double TraceSamplingRatio { get; set; } = 1.0;
    public int MetricExportIntervalMs { get; set; } = 30000;
    public bool EnableConsoleExporter { get; set; } = true;
    public string[] CustomDimensions { get; set; } = [];
}

public class ApplicationInsightsSettings
{
    public bool Enabled { get; set; } = false;
    public string? ConnectionString { get; set; }
    public string? InstrumentationKey { get; set; }
    public bool EnableDependencyTracking { get; set; } = true;
    public bool EnableExceptionTracking { get; set; } = true;
    public bool EnablePerformanceCounters { get; set; } = true;
}

public class PrometheusSettings
{
    public bool Enabled { get; set; } = false;
    public int Port { get; set; } = 9090;
    public string Path { get; set; } = "/metrics";
    public string[] AllowedIPs { get; set; } = ["127.0.0.1", "::1"];
}

public class JaegerSettings
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6831;
    public string? ServiceName { get; set; }
}