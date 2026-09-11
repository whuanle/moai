using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查全网搜索请求参数.
/// </summary>
public class BoChaWebSearchRequest
{
    /// <summary>
    /// 用户的搜索词.
    /// </summary>
    [Description("用户的搜索词")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 搜索指定时间范围内的网页.
    /// </summary>
    [Description("搜索时间范围：noLimit（默认）、oneDay、oneWeek、oneMonth、oneYear，或 YYYY-MM-DD..YYYY-MM-DD 日期区间")]
    public string Freshness { get; set; } = "noLimit";

    /// <summary>
    /// 是否返回网页文本摘要.
    /// </summary>
    [Description("是否返回网页文本摘要；开启后摘要更完整，但响应体积更大，默认 false")]
    public bool Summary { get; set; }

    /// <summary>
    /// 返回结果的条数.
    /// </summary>
    [Description("返回结果的条数，可填 1-50，默认 10")]
    public int Count { get; set; } = 10;

    /// <summary>
    /// 指定搜索的网站范围.
    /// </summary>
    [Description("仅在这些网站内搜索，多个域名用 | 或 , 分隔（最多 100 个），例如 qq.com|m.163.com；不填表示不限制")]
    public string? Include { get; set; }

    /// <summary>
    /// 排除搜索的网站范围.
    /// </summary>
    [Description("排除这些网站，多个域名用 | 或 , 分隔（最多 100 个）；不填表示不排除")]
    public string? Exclude { get; set; }
}
