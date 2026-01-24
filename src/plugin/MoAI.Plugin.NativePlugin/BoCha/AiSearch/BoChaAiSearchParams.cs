#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.BoCha.AiSearch;

/// <summary>
/// AI Search 请求参数
/// </summary>
public class BoChaAiSearchParams
{
    /// <summary>
    /// 用户的搜索内容。
    /// </summary>
    [JsonPropertyName("query")]
    [NativePluginField(
        Key = nameof(Query),
        Description = "用户的搜索内容",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "C#可以做什么")]
    public string Query { get; init; }

    /// <summary>
    /// 搜索指定时间范围内的网页.
    /// 可填值：
    /// - oneDay：一天内
    /// - oneWeek：一周内
    /// - oneMonth：一个月内
    /// - oneYear：一年内
    /// - noLimit：不限（默认）
    /// </summary>
    [JsonPropertyName("freshness")]
    [NativePluginField(
        Key = nameof(Freshness),
        Description = "搜索指定时间范围内的网页",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "oneDay：一天内 | oneWeek：一周内 | oneMonth：一个月内 | oneYear：一年内 | noLimit：不限（默认）")]
    public string Freshness { get; init; } = "noLimit";

    /// <summary>
    /// 每次搜索返回搜索结果的数量，最多50条。默认10条。
    /// （返回的结果数量有可能小于指定的 count 值）
    /// </summary>
    [JsonPropertyName("count")]
    [NativePluginField(
        Key = nameof(Count),
        Description = "每次搜索返回搜索结果的数量，最多50条",
        FieldType = PluginConfigFieldType.Number,
        IsRequired = false,
        ExampleValue = "10")]
    public int Count { get; init; } = 10;

    /// <summary>
    /// 是否使用大模型进行回答。默认为 true。
    /// - false：关闭大模型回答，不再输出总结答案和追问问题。
    /// - true：使用大模型回答，接口返回总结答案、追问问题。
    /// </summary>
    [JsonPropertyName("answer")]
    [NativePluginField(
        Key = nameof(Answer),
        Description = "是否使用大模型进行回答,默认为 true",
        FieldType = PluginConfigFieldType.Boolean,
        IsRequired = false,
        ExampleValue = "false")]
    public bool Answer { get; init; } = true;

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
    [NativePluginField(
        Key = nameof(Include),
        Description = "指定搜索的 site 范围,多个域名使用 | 或 , 分隔",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "site:qq.com|m.163.co")]
    public string? Include { get; init; }
}