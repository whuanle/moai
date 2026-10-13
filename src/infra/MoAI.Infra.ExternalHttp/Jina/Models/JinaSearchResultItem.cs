#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Jina.Models;

/// <summary>
/// Jina Search API 单条搜索结果：Jina 会抓取命中页面并转换为 LLM 友好的正文.
/// </summary>
public class JinaSearchResultItem
{
    /// <summary>
    /// 页面标题.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 页面摘要描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 页面地址.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 页面正文（默认 Markdown）.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 本条结果正文转换消耗的 token 数.
    /// </summary>
    public JinaUsage? Usage { get; init; }
}
