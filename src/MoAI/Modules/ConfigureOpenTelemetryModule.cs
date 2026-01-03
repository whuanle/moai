using Maomi;
using Maomi.MQ;
using MoAI.Infra;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MoAI.Modules;

/// <summary>
/// 可观察性.
/// </summary>
public class ConfigureOpenTelemetryModule : IModule
{
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureOpenTelemetryModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public ConfigureOpenTelemetryModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddOpenTelemetry()
              .ConfigureResource(resource => resource.AddService(AppConst.ActivitySource.Name))
              .WithTracing(tracing =>
              {
                  tracing.AddMaomiMQInstrumentation(options =>
                  {
                      options.Sources.AddRange(MaomiMQDiagnostic.Sources);
                      options.RecordException = true;
                  })
                  .AddAspNetCoreInstrumentation()
                  .AddEntityFrameworkCoreInstrumentation()
                  .AddHttpClientInstrumentation()
                  .AddRedisInstrumentation()
                  .AddOtlpExporter(options =>
                  {
                      options.Endpoint = new Uri(_systemOptions.OTLP.Trace);
                      options.Protocol = (OtlpExportProtocol)_systemOptions.OTLP.Protocol;
                  });
              })
              .WithMetrics(metrices =>
              {
                  metrices.AddAspNetCoreInstrumentation()
                  .AddMaomiMQInstrumentation()
                  .AddHttpClientInstrumentation()
                  .AddAspNetCoreInstrumentation()
                  .AddRuntimeInstrumentation()
                  .AddOtlpExporter(options =>
                  {
                      options.Endpoint = new Uri(_systemOptions.OTLP.Metrics);
                      options.Protocol = (OtlpExportProtocol)_systemOptions.OTLP.Protocol;
                  });
              });
    }
}
