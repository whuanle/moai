using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickHouse 查询插件请求参数.
/// </summary>
public class ClickHouseQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：sql=只读 SQL（默认）/ traces=OpenTelemetry 链路 / logs=OpenTelemetry 日志 / metrics=OpenTelemetry 指标；未知值会被拒绝")]
    public string Mode { get; set; } = "sql";

    /// <summary>
    /// 只读 SQL.
    /// </summary>
    [Description("sql 模式：要执行的只读表达式（单条 SELECT/WITH/SHOW/DESCRIBE 等读语句；末尾 FORMAT 子句会被剥离）")]
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// traces 模式：TraceID 精确过滤.
    /// </summary>
    [Description("traces 模式：按 TraceID 精确过滤")]
    public string? TraceId { get; set; }

    /// <summary>
    /// 服务名（service.name）过滤.
    /// </summary>
    [Description("traces/logs/metrics 模式：按 service.name 精确过滤")]
    public string? ServiceName { get; set; }

    /// <summary>
    /// traces 模式：span 名称过滤.
    /// </summary>
    [Description("traces 模式：按 span 名称精确过滤")]
    public string? SpanName { get; set; }

    /// <summary>
    /// traces 模式：最小 HTTP/OTel 状态码.
    /// </summary>
    [Description("traces 模式：StatusCode 大于等于该值的 span（>0 表示只看异常 span）；列不存在时被忽略")]
    public int MinStatusCode { get; set; }

    /// <summary>
    /// logs 模式：严重级文本过滤.
    /// </summary>
    [Description("logs 模式：按 SeverityText 精确过滤，如 ERROR/WARN")]
    public string? SeverityText { get; set; }

    /// <summary>
    /// logs 模式：正文子串搜索.
    /// </summary>
    [Description("logs 模式：Body 子串大小写不敏感搜索；Body 为 Map 形态时被忽略并在 Notice 说明")]
    public string? SearchText { get; set; }

    /// <summary>
    /// metrics 模式：指标名过滤.
    /// </summary>
    [Description("metrics 模式：按指标名精确过滤")]
    public string? MetricName { get; set; }

    /// <summary>
    /// metrics 模式：otel_metrics_* 表名（可选）.
    /// </summary>
    [Description("metrics 模式：指定 otel_metrics_* 表名（gauge/sum/histogram/summary 等）；缺省时库中只有一张时自动选用，多张时列出候选")]
    public string? Table { get; set; }

    /// <summary>
    /// 起始时间（可空）.
    /// </summary>
    [Description("traces/logs/metrics 模式：开始时间（RFC3339，如 2026-09-26T08:00:00+08:00 或 Z 结尾）")]
    public string? TimeFrom { get; set; }

    /// <summary>
    /// 结束时间（可空）.
    /// </summary>
    [Description("traces/logs/metrics 模式：结束时间（RFC3339）")]
    public string? TimeTo { get; set; }

    /// <summary>
    /// 最多返回行数（可空，缺省用配置）.
    /// </summary>
    [Description("最多返回行数，取值 1-1000（缺省用配置 MaxRows）；已被配置上限压紧")]
    public int? Limit { get; set; }
}
