using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 协作面处理器基类：请求体缓冲（保证 401 重试可重放）与响应克隆、错误体转换工具。
/// </summary>
public abstract class SubsysHandlerBase : DelegatingHandler
{
    /// <summary>缓冲请求体为字节数组，保证请求可重放。</summary>
    /// <param name="request">请求消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    protected static async Task BufferRequestContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return;
        }

        var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var buffered = new ByteArrayContent(bytes);
        foreach (var header in request.Content.Headers)
        {
            buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        request.Content = buffered;
    }

    /// <summary>克隆请求（保留头与选项）。</summary>
    /// <param name="request">原请求。</param>
    /// <returns>克隆请求。</returns>
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "克隆请求所有权转移给基类管道发送")]
    protected static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            Content = request.Content,
        };
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var property in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(property.Key), property.Value);
        }

        return clone;
    }

    /// <summary>非 2xx 响应读取错误体并转换为 <see cref="SubsysException"/>；2xx 原样返回。</summary>
    /// <param name="response">响应消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功响应原样返回；失败时抛出异常不返回。</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:Do not catch general exception types", Justification = "错误体读取失败不应掩盖原始状态码")]
    protected static async Task<HttpResponseMessage> EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        string? content = null;
        if (response.Content is not null)
        {
            try
            {
                content = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception)
            {
                // 错误体读取失败不掩盖状态码
            }
        }

        throw SubsysException.From(response.StatusCode, content);
    }
}
