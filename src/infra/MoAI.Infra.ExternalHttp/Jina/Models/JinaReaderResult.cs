#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Jina.Models;

/// <summary>
/// Jina Reader API 阅读结果：目标页面转换后的 LLM 友好内容.
/// </summary>
public class JinaReaderResult
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
    /// 实际阅读的页面地址（重定向后）.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 页面正文（默认 Markdown）.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 页面发布时间（页面可识别时返回）.
    /// </summary>
    public string? PublishTime { get; init; }

    /// <summary>
    /// 链接摘要（X-With-Links-Summary 开启时返回）：地址 → 锚文本.
    /// </summary>
    public Dictionary<string, string>? Links { get; init; }

    /// <summary>
    /// 图片摘要（X-With-Images-Summary 开启时返回）：图片地址 → 说明.
    /// </summary>
    public Dictionary<string, string>? Images { get; init; }

    /// <summary>
    /// 本次转换消耗的 token 数.
    /// </summary>
    public JinaUsage? Usage { get; init; }
}
