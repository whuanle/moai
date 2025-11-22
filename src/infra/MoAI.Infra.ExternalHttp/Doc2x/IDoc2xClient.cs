using MoAI.Infra.Doc2x.Models;
using MoAI.Infra.Models;
using Refit;

namespace MoAI.Infra.Doc2x;

/// <summary>
/// Doc2X API v2 客户端接口
/// </summary>
public interface IDoc2xClient
{
    /// <summary>
    /// 获取 HttpClient 实例
    /// </summary>
    HttpClient Client { get; }

    /// <summary>
    /// 直接识别，pdf 在 300mb 内
    /// </summary>
    /// <param name="key"></param>
    /// <param name="stream"></param>
    /// <returns>返回结果不包含 url.</returns>
    [Post("/api/v2/parse/pdf")]
    Task<Doc2xPreuploadResponse> ParsePdfAsync([Header("Authorization")] string key, [Body(buffered: true)] StreamPart stream);

    /// <summary>
    /// 文件预上传接口
    /// </summary>
    /// <param name="key"></param>
    /// <param name="dto"></param>
    /// <returns>包含上传URL和UID的响应数据</returns>
    [Post("/api/v2/parse/preupload")]
    Task<Doc2xPreuploadResponse> PreuploadAsync([Header("Authorization")] string key, [Body(BodySerializationMethod.Serialized)] EmptyDto dto);

    /// <summary>
    /// 获取解析状态接口
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uid">任务的唯一标识符</param>
    /// <returns>任务状态的响应数据</returns>
    [Get("/api/v2/parse/status")]
    Task<Doc2xParseStatusResponse> GetParseStatusAsync([Header("Authorization")] string key, [Query] string uid);

    /// <summary>
    /// 导出解析结果接口
    /// </summary>
    /// <param name="key"></param>
    /// <param name="request">导出请求参数</param>
    /// <returns>导出任务的响应数据</returns>
    [Post("/api/v2/convert/parse")]
    Task<Doc2xExportResponse> ExportParseResultAsync([Header("Authorization")] string key, [Body] Doc2xExportRequest request);

    /// <summary>
    /// 获取导出结果接口
    /// </summary>
    /// <param name="key"></param>
    /// <param name="uid">导出任务的唯一标识符</param>
    /// <returns>导出结果的响应数据</returns>
    [Get("/api/v2/convert/parse/result")]
    Task<Doc2xExportResultResponse> GetExportResultAsync([Header("Authorization")] string key, [Query] string uid);
}