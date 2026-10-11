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
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureOpenTelemetryModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public ConfigureOpenTelemetryModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var traceEndpoint = ParseOtlpEndpoint(_systemOptions.OTLP.Trace);
        var metricsEndpoint = ParseOtlpEndpoint(_systemOptions.OTLP.Metrics);

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
                  .AddRedisInstrumentation();

                  // Debug=true 时监听 AI 应用埋点事件源（AiObservabilityMiddleware 的 UseOpenTelemetry sourceName 配对）
                  if (_systemOptions.Debug)
                  {
                      tracing.AddSource(AppConst.AiTraceSourceName);
                  }

                  // OTLP 为可选项：未配置或地址非法时跳过导出，避免空地址 new Uri 崩溃
                  if (traceEndpoint != null)
                  {
                      tracing.AddOtlpExporter(options =>
                      {
                          options.Endpoint = traceEndpoint;
                          options.Protocol = (OtlpExportProtocol)_systemOptions.OTLP.Protocol;
                      });
                  }
              })
              .WithMetrics(metrices =>
              {
                  metrices.AddAspNetCoreInstrumentation()
                  .AddMaomiMQInstrumentation()
                  .AddHttpClientInstrumentation()
                  .AddAspNetCoreInstrumentation()
                  .AddRuntimeInstrumentation();

                  // Debug=true 时收集 MEAI 对话层 gen_ai 指标（调用耗时与 token 用量）；MEAI 10.9 的 meter 名即 sourceName
                  if (_systemOptions.Debug)
                  {
                      metrices.AddMeter(AppConst.AiTraceSourceName);
                  }

                  if (metricsEndpoint != null)
                  {
                      metrices.AddOtlpExporter(options =>
                      {
                          options.Endpoint = metricsEndpoint;
                          options.Protocol = (OtlpExportProtocol)_systemOptions.OTLP.Protocol;
                      });
                  }
              });
    }

    /// <summary>
    /// 解析 OTLP 端点，空白或非法地址返回 null（视为未启用导出）.
    /// </summary>
    /// <param name="endpoint">配置的端点地址.</param>
    /// <returns>返回 <see cref="Uri"/>，未配置时为 null.</returns>
    private static Uri? ParseOtlpEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri : null;
    }
}
