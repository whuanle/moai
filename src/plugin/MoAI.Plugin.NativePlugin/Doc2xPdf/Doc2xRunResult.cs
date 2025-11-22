using MoAI.Infra.Doc2x.Models;

namespace MoAI.Plugin.Doc2xPdf;

/// <summary>
/// 响应结果.
/// </summary>
public class Doc2xRunResult : Doc2xCode
{
    /// <summary>
    /// 错误信息.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Markdown 内容.
    /// </summary>
    public string? MarkdownContent { get; init; }
}
