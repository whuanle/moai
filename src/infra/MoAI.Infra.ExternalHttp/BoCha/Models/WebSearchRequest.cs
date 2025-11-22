#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search 请求参数
/// </summary>
public class WebSearchRequest
{
    /// <summary>
    /// 用户的搜索词。
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; init; }

    /// <summary>
    /// 是否显示文本摘要。
    /// 可填值：
    /// - true，显示
    /// - false，不显示（默认）
    /// </summary>
    [JsonPropertyName("summary")]
    public bool? Summary { get; init; } = false;

    /// <summary>
    /// 搜索指定时间范围内的网页。
    /// 可填值：
    /// - noLimit，不限（默认）
    /// - oneDay，一天内
    /// - oneWeek，一周内
    /// - oneMonth，一个月内
    /// - oneYear，一年内
    /// - YYYY-MM-DD..YYYY-MM-DD，搜索日期范围，例如："2025-01-01..2025-04-06"
    /// - YYYY-MM-DD，搜索指定日期，例如："2025-04-06"
    /// 推荐使用“noLimit”。搜索算法会自动进行时间范围的改写，效果更佳。
    /// 如果指定时间范围，很有可能出现时间范围内没有相关网页的情况，导致找不到搜索结果。
    /// </summary>
    [JsonPropertyName("freshness")]
    public string Freshness { get; init; } = "noLimit";

    /// <summary>
    /// 返回结果的条数（实际返回结果数量可能会小于 count 指定的数量）。
    /// 可填范围：1-50，最大单次搜索返回 50 条。
    /// 默认为 10。
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; init; } = 10;

    /// <summary>
    /// 指定搜索的网站范围。
    /// 多个域名使用 | 或 , 分隔，最多不能超过 100 个。
    /// 可填值：
    /// - 根域名
    /// - 子域名
    /// 例如：qq.com|m.163.com
    /// </summary>
    [JsonPropertyName("include")]
    public string Include { get; init; }

    /// <summary>
    /// 排除搜索的网站范围。
    /// 多个域名使用 | 或 , 分隔，最多不能超过 100 个。
    /// 可填值：
    /// - 根域名
    /// - 子域名
    /// 例如：qq.com|m.163.com
    /// </summary>
    [JsonPropertyName("exclude")]
    public string Exclude { get; init; }
}