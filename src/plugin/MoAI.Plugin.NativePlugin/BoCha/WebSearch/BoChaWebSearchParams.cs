#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.BoCha.WebSearch;

/// <summary>
/// Web Search 请求参数
/// </summary>
public class BoChaWebSearchParams
{
    /// <summary>
    /// 用户的搜索词。
    /// </summary>
    [JsonPropertyName("query")]
    [NativePluginField(
        Key = nameof(Query),
        Description = "用户的搜索词",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "如何学习 C#")]
    public string Query { get; init; }

    /// <summary>
    /// 是否返回文本摘要。
    /// </summary>
    [JsonPropertyName("summary")]
    [NativePluginField(
        Key = nameof(Summary),
        Description = "是否返回文本摘要",
        FieldType = PluginConfigFieldType.Boolean,
        IsRequired = false,
        ExampleValue = "true")]
    public bool? Summary { get; init; } = false;

    /// <summary>
    /// 搜索指定时间范围内的网页。
    /// </summary>
    [JsonPropertyName("freshness")]
    [NativePluginField(
        Key = nameof(Freshness),
        Description = "搜索指定时间范围",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "noLimit、oneMonth、YYYY-MM-DD..YYYY-MM-DD")]
    public string Freshness { get; init; } = "noLimit";

    /// <summary>
    /// 返回结果的条数。
    /// </summary>
    [JsonPropertyName("count")]
    [NativePluginField(
        Key = nameof(Count),
        Description = "返回结果的条数 (1-50)",
        FieldType = PluginConfigFieldType.Number,
        IsRequired = false,
        ExampleValue = "10")]
    public int Count { get; init; } = 10;

    /// <summary>
    /// 指定搜索的网站范围。
    /// </summary>
    [JsonPropertyName("include")]
    [NativePluginField(
        Key = nameof(Include),
        Description = "指定搜索的网站范围，多个域名使用 | 或 , 分隔",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "qq.com|m.163.com")]
    public string? Include { get; init; }

    /// <summary>
    /// 排除搜索的网站范围。
    /// </summary>
    [JsonPropertyName("exclude")]
    [NativePluginField(
        Key = nameof(Exclude),
        Description = "排除搜索的网站范围，多个域名使用 | 或 , 分隔",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "m.163.com")]
    public string? Exclude { get; init; }}
