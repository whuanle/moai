using System.Net;
using System.Text;
using System.Text.Json;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 子系统协作面调用失败异常。由客户端管道在收到非 2xx 响应时抛出，
/// 携带服务端错误语义（401 invalid_token / 403 subsys_app_disabled 等）。
/// </summary>
public class SubsysException : Exception
{
    /// <summary>HTTP 状态码。</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>错误类型（服务端 <c>error.type</c> 字段，如 authentication_error / permission_error），可能为空。</summary>
    public string? ErrorType { get; }

    /// <summary>错误码（服务端 <c>error.code</c> 字段，如 invalid_token / subsys_app_disabled），可能为空。</summary>
    public string? ErrorCode { get; }

    /// <summary>原始响应体文本，可能为空。</summary>
    public string? RawContent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysException"/> class.
    /// </summary>
    public SubsysException()
        : this(HttpStatusCode.InternalServerError, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysException"/> class.
    /// </summary>
    /// <param name="message">错误消息。</param>
    public SubsysException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysException"/> class.
    /// </summary>
    /// <param name="message">错误消息。</param>
    /// <param name="innerException">内部异常。</param>
    public SubsysException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysException"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="message">错误消息。</param>
    /// <param name="errorType">服务端 error.type 字段。</param>
    /// <param name="errorCode">服务端 error.code 字段。</param>
    /// <param name="rawContent">原始响应体文本。</param>
    public SubsysException(HttpStatusCode statusCode, string? message, string? errorType = null, string? errorCode = null, string? rawContent = null)
        : base(message ?? $"子系统协作面请求失败：{(int)statusCode} {statusCode}")
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        ErrorCode = errorCode;
        RawContent = rawContent;
    }

    /// <summary>
    /// 将非成功响应转换为 <see cref="SubsysException"/>。识别服务端两种错误体：
    /// <c>{error:{type,code,message}}</c>（协作面中间件）与 <c>{message}</c> / <c>{errors:[...],name}</c>（业务/校验错误）。
    /// </summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="rawContent">原始响应体文本。</param>
    /// <returns>带服务端错误语义的异常。</returns>
    public static SubsysException From(HttpStatusCode statusCode, string? rawContent)
    {
        var (message, errorType, errorCode) = Decode(rawContent);
        return new SubsysException(statusCode, message, errorType, errorCode, rawContent);
    }

    /// <summary>尝试从响应体解析错误信息。</summary>
    private static (string? Message, string? ErrorType, string? ErrorCode) Decode(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return (null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawContent);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null);
            }

            // 协作面中间件错误：{error:{type,code,message}}
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                return (
                    error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null,
                    error.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
                    error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null);
            }

            // 业务/校验错误：{message} 或 {errors:[...],name}
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return (message.GetString(), null, null);
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var builder = new StringBuilder();
                foreach (var item in errors.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        builder.Append(item.GetString()).Append("; ");
                    }
                }

                return (builder.Length > 0 ? builder.ToString(0, builder.Length - 2) : null, null, null);
            }

            return (null, null, null);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }
}
