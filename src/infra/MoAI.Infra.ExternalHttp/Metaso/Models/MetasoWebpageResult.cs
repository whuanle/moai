#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Metaso.Models;

/// <summary>
/// 秘塔搜索 API 单条网页结果.
/// </summary>
public class MetasoWebpageResult
{
    /// <summary>
    /// 页面标题.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// 相关度得分.
    /// </summary>
    public string? Score { get; init; }

    /// <summary>
    /// 内容摘要（conciseSnippet=true 时为精简原文匹配信息）.
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// 结果序号.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    public string? Date { get; init; }

    /// <summary>
    /// 来源网页原文（includeRawContent=true 时返回）.
    /// </summary>
    public string? RawContent { get; init; }
}
