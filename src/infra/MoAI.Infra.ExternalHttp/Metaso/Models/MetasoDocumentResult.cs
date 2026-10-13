#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Metaso.Models;

/// <summary>
/// 秘塔搜索 API 单条文库/视频结果（两者字段同构：标题、链接、摘要、序号）.
/// </summary>
public class MetasoDocumentResult
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 地址.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// 内容摘要.
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// 结果序号.
    /// </summary>
    public int? Position { get; init; }
}
