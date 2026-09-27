using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Tempo 链路检索摘要条目.
/// </summary>
public class TempoTraceSummary
{
    /// <summary>
    /// TraceID.
    /// </summary>
    [Description("TraceID（hex）")]
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 根 span 的服务名.
    /// </summary>
    [Description("根 span 的服务名")]
    public string RootServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 根 span 名称.
    /// </summary>
    [Description("根 span 名称")]
    public string RootTraceName { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间（Unix 纳秒文本）.
    /// </summary>
    [Description("开始时间（Unix 纳秒文本）")]
    public string StartTimeUnixNano { get; set; } = string.Empty;

    /// <summary>
    /// 总时长（毫秒文本）.
    /// </summary>
    [Description("总时长（毫秒文本）")]
    public string DurationMs { get; set; } = string.Empty;
}
