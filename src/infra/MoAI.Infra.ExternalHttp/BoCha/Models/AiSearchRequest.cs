#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// AI Search 请求参数
/// </summary>
public class AiSearchRequest
{
    /// <summary>
    /// 用户的搜索内容。
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; init; }

    /// <summary>
    /// 搜索指定时间范围内的网页。
    /// 可填值：
    /// - oneDay：一天内
    /// - oneWeek：一周内
    /// - oneMonth：一个月内
    /// - oneYear：一年内
    /// - noLimit：不限（默认）
    /// </summary>
    [JsonPropertyName("freshness")]
    public string Freshness { get; init; } = "noLimit";

    /// <summary>
    /// 每次搜索返回搜索结果的数量，最多50条。默认10条。
    /// （返回的结果数量有可能小于指定的 count 值）
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; init; } = 10;

    /// <summary>
    /// 是否使用大模型进行回答。默认为 true。
    /// - false：关闭大模型回答，不再输出总结答案和追问问题。
    /// - true：使用大模型回答，接口返回总结答案、追问问题。
    /// </summary>
    [JsonPropertyName("answer")]
    public bool Answer { get; init; } = true;

    /// <summary>
    /// 是否启用流式返回。默认为 false。
    /// - false：非流式响应，所有响应准备好后再返回。
    /// - true：流式响应，实时返回响应内容，客户端需组装最终回复。
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = false;

    /// <summary>
    /// 指定搜索的 site 范围。多个域名使用 | 或 , 分隔，最多不能超过100个。
    /// 可填值：
    /// - 根域名
    /// - 子域名
    /// 例如：qq.com|m.163.com
    /// 也可以在 query 中使用 site 方式来指定搜索范围。
    /// 例如："query" : "site:qq.com|m.163.com 阿里ESG报告"
    /// </summary>
    [JsonPropertyName("include")]
    public string? Include { get; init; }
}