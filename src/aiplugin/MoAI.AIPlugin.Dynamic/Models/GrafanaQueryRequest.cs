using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Grafana 查询插件请求参数.
/// </summary>
public class GrafanaQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：health=连通性与版本（默认）/ annotations=注解（事故时间线）/ search=仪表板搜索；未知值会被拒绝")]
    public string Mode { get; set; } = "health";

    /// <summary>
    /// annotations 模式：起始时间.
    /// </summary>
    [Description("annotations 模式：起始时间（RFC3339 如 2026-09-29T00:00:00Z，或毫秒时间戳），可空=不限")]
    public string? From { get; set; }

    /// <summary>
    /// annotations 模式：结束时间.
    /// </summary>
    [Description("annotations 模式：结束时间（RFC3339 或毫秒时间戳），可空=不限")]
    public string? To { get; set; }

    /// <summary>
    /// annotations 模式：标签过滤.
    /// </summary>
    [Description("annotations 模式：标签过滤（逗号分隔），如 alerting,deploy")]
    public string? Tags { get; set; }

    /// <summary>
    /// search 模式：关键字.
    /// </summary>
    [Description("search 模式：仪表板搜索关键字（可空=全部）")]
    public string? Query { get; set; }
}
