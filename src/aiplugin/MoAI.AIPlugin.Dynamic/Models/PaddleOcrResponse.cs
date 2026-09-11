using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PP-OCRv5 通用文字识别响应结果.
/// </summary>
/// <remarks>
/// 按页聚合 <c>OcrResults</c>，每页对应一份识别结果；文字内容由 <c>rec_texts</c> 用换行拼接.
/// </remarks>
public class PaddleOcrResponse
{
    /// <summary>
    /// 按页聚合的识别结果.
    /// </summary>
    [Description("按页聚合的识别结果列表；顺序与 PaddleOCR 服务端返回的 ocrResults 一致")]
    public IReadOnlyList<PaddleOcrPage> Pages { get; init; } = [];
}