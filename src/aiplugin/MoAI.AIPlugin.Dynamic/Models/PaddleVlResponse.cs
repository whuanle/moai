using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PaddleOCR-VL 视觉语言模型文档解析响应结果.
/// </summary>
/// <remarks>
/// 按页聚合 <c>LayoutParsingResults</c>，每页对应一份版面解析结果；Markdown 文本与图片分别抽取，
/// Markdown 图片以「相对路径 → Base64」字典回传，方便上游拼装还原.
/// </remarks>
public class PaddleVlResponse
{
    /// <summary>
    /// 按页聚合的视觉语言模型解析结果.
    /// </summary>
    [Description("按页聚合的视觉语言模型解析结果列表；顺序与 PaddleOCR 服务端返回的 layoutParsingResults 一致")]
    public IReadOnlyList<PaddleVlPage> Pages { get; init; } = [];
}