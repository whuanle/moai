using MoAI.Infra.Paddleocr.Models;
using Refit;

namespace MoAI.Infra.Paddleocr;

/// <summary>
/// 飞桨OCR客户端接口，支持PDF/PNG/JPG/BMP/TIF等各种图片格式.
/// </summary>
public interface IPaddleocrClient
{
    /// <summary>
    /// HttpClient.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// 获取图像OCR结果,适合 PP-OCRv5 模型.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="request">OCR请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>OCR响应结果.</returns>
    [Post("/ocr")]
    Task<PaddleOcrResponse<PaddleOcrResult>> OcrAsync([Header("token")] string token, [Body] PaddleOcrRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 文档解析 (Layout Parsing), 适合 PP-StructureV3 模型.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="request">文档解析请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文档解析响应结果.</returns>
    [Post("/layout-parsing")]
    Task<PaddleOcrResponse<PaddleLayoutParsingResult>> StructureV3Async([Header("token")] string token, [Body] PaddleLayoutParsingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 文档解析 (PaddleOCR-VL), 适合 PaddleOCR-VL 模型.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="request">文档解析请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文档解析响应结果.</returns>
    [Post("/layout-parsing")]
    Task<PaddleOcrResponse<PaddleLayoutParsingResult>> LayoutParsingVlAsync([Header("token")] string token, [Body] PaddleOcrVlRequest request, CancellationToken cancellationToken = default);
}
