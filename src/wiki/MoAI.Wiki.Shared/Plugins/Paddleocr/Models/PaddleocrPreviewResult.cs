namespace MoAI.Wiki.Plugins.Paddleocr.Models;

/// <summary>
/// 飞桨 OCR 预览结果.
/// </summary>
public class PaddleocrPreviewResult
{
    /// <summary>
    /// 解析后的 Markdown 内容.
    /// </summary>
    public IReadOnlyCollection<string> Texts { get; init; } = default!;

    /// <summary>
    /// OCR 处理后的图片列表 (Base64 或 URL).
    /// </summary>
    public IReadOnlyCollection<string> Images { get; init; } = Array.Empty<string>();
}
