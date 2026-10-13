#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Metaso.Models;

/// <summary>
/// 秘塔网页阅读 API 结构化响应（Accept: application/json；text/plain 时响应体即 Markdown 文本）.
/// </summary>
public class MetasoReaderResult
{
    /// <summary>
    /// 页面标题.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 页面地址.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 作者.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// 发布日期.
    /// </summary>
    public string? Date { get; init; }

    /// <summary>
    /// Markdown 正文.
    /// </summary>
    public string? Markdown { get; init; }

    /// <summary>
    /// 正文（部分响应以 content 承载）.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 本次调用消耗的点数.
    /// </summary>
    public int? Credits { get; init; }
}
