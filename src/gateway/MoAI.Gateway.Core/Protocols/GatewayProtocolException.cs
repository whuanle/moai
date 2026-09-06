using System.Text.Json;

namespace MoAI.Gateway.Protocols;

/// <summary>
/// 协议解析/组装异常，映射为对应入口协议的错误响应.
/// </summary>
public class GatewayProtocolException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayProtocolException"/> class.
    /// </summary>
    /// <param name="statusCode">http 状态码.</param>
    /// <param name="message">错误信息.</param>
    /// <param name="code">错误码.</param>
    public GatewayProtocolException(int statusCode, string message, string? code = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>
    /// http 状态码.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 错误码.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// 上游错误（透传上游状态码）.
    /// </summary>
    /// <param name="statusCode">上游状态码.</param>
    /// <param name="body">上游响应体.</param>
    /// <returns>返回异常实例.</returns>
    public static GatewayProtocolException Upstream(int statusCode, string body)
    {
        var message = ExtractUpstreamMessage(body) ?? $"Upstream error (HTTP {statusCode}).";
        return new GatewayProtocolException(statusCode, message, "upstream_error");
    }

    private static string? ExtractUpstreamMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString();
                    }

                    if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString();
                    }
                }

                if (root.TryGetProperty("message", out var rootMessage) && rootMessage.ValueKind == JsonValueKind.String)
                {
                    return rootMessage.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

/// <summary>
/// JSON 读取助手.
/// </summary>
internal static class GatewayJsonReader
{
    /// <summary>
    /// 读取字符串属性.
    /// </summary>
    public static string? String(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// 读取 double 属性.
    /// </summary>
    public static double? Double(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) ? d : null;
    }

    /// <summary>
    /// 读取 int 属性.
    /// </summary>
    public static int? Int(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) ? i : null;
    }

    /// <summary>
    /// 读取 bool 属性.
    /// </summary>
    public static bool? Bool(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null;
    }

    /// <summary>
    /// 读取嵌套对象属性.
    /// </summary>
    public static JsonElement? Object(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return value.Clone();
        }

        return null;
    }

    /// <summary>
    /// 取数组首元素，空数组或 null 返回 default.
    /// </summary>
    public static JsonElement First(JsonElement? element)
    {
        return element.HasValue ? element.Value.EnumerateArray().FirstOrDefault() : default;
    }

    /// <summary>
    /// 读取数组属性.
    /// </summary>
    public static JsonElement? Array(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Array)
        {
            return value.Clone();
        }

        return null;
    }
}

/// <summary>
/// 用量估算：上游未返回 usage 时按字符数近似（约 4 字符/token），保证额度可计量.
/// </summary>
public static class GatewayUsageEstimator
{
    /// <summary>
    /// 估算一次请求的用量.
    /// </summary>
    /// <param name="request">归一化请求.</param>
    /// <param name="outputText">输出文本.</param>
    /// <returns>返回估算用量.</returns>
    public static GatewayUsage Estimate(GatewayChatRequest request, string? outputText)
    {
        var promptChars = 0;
        foreach (var message in request.Messages)
        {
            promptChars += message.Text?.Length ?? 0;
            if (message.Parts != null)
            {
                foreach (var part in message.Parts.OfType<GatewayTextPart>())
                {
                    promptChars += part.Text.Length;
                }
            }

            if (message.ToolCalls != null)
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    promptChars += toolCall.ArgumentsJson.Length + toolCall.Name.Length;
                }
            }
        }

        var completionChars = outputText?.Length ?? 0;
        return new GatewayUsage(Math.Max(1, promptChars / 4), Math.Max(1, completionChars / 4));
    }
}
