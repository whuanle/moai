using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Grafana 查询插件响应（按 Mode 只填充对应字段）.
/// </summary>
public class GrafanaQueryResponse
{
    /// <summary>
    /// 结果类型：health / annotations / search.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// health 模式：服务端返回的原文（含 database/version 等）.
    /// </summary>
    public Dictionary<string, string>? Health { get; set; }

    /// <summary>
    /// annotations 模式：注解列表.
    /// </summary>
    public IReadOnlyList<GrafanaAnnotation> Annotations { get; set; } = new List<GrafanaAnnotation>();

    /// <summary>
    /// search 模式：仪表板列表.
    /// </summary>
    public IReadOnlyList<GrafanaDashboard> Dashboards { get; set; } = new List<GrafanaDashboard>();

    /// <summary>
    /// 列表是否因超过 MaxListItems 被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Grafana 注解（事故时间线条目）.
/// </summary>
public class GrafanaAnnotation
{
    /// <summary>
    /// 注解 ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 文本.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 标签.
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// 开始时间（ISO 8601 UTC）.
    /// </summary>
    public string TimeFrom { get; set; } = string.Empty;

    /// <summary>
    /// 结束时间（ISO 8601 UTC，可空）.
    /// </summary>
    public string TimeTo { get; set; } = string.Empty;

    /// <summary>
    /// 所属仪表板 ID.
    /// </summary>
    public long DashboardId { get; set; }

    /// <summary>
    /// 所属面板 ID.
    /// </summary>
    public long PanelId { get; set; }
}

/// <summary>
/// Grafana 仪表板（搜索结果条目）.
/// </summary>
public class GrafanaDashboard
{
    /// <summary>
    /// 仪表板 ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 仪表板 UID.
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 相对地址.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 类型（dash-db / dash-folder）.
    /// </summary>
    public string Type { get; set; } = string.Empty;
}
