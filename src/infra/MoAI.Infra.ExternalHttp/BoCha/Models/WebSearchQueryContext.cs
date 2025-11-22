#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search Query Context
/// </summary>
public class WebSearchQueryContext
{
    /// <summary>
    /// 原始的搜索关键字
    /// </summary>
    [JsonPropertyName("originalQuery")]
    public string OriginalQuery { get; init; }
}
