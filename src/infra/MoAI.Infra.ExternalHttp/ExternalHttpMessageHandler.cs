#pragma warning disable SA1118 // Parameter should not span multiple lines
#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace MoAI.Infra;

/// <summary>
/// 外部请求拦截器.
/// </summary>
public class ExternalHttpMessageHandler : DelegatingHandler
{
    private readonly ILogger<ExternalHttpMessageHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public ExternalHttpMessageHandler(ILogger<ExternalHttpMessageHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var activity = AppConst.ActivitySource.StartActivity("AiStreamHttpMessageHandler");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            await PrintLog(request, response, stopwatch.Elapsed, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await PrintExceptionLog(request, ex, stopwatch.Elapsed);
            throw;
        }
    }

    private async Task PrintExceptionLog(HttpRequestMessage request, Exception ex, TimeSpan duration)
    {
        var requestContent = await GetRequestContentAsync(request);
        var requestHeaders = GetHeaders(request.Headers, request.Content?.Headers);

        Activity.Current?.SetTag("http.request.method", request.Method.ToString());
        Activity.Current?.SetTag("http.request.uri", request.RequestUri?.ToString());
        Activity.Current?.SetTag("http.request.duration_ms", duration.TotalMilliseconds);
        Activity.Current?.SetTag("error.type", ex.GetType().FullName);
        Activity.Current?.SetTag("error.message", ex.Message);

        var logData = new Dictionary<string, object?>
        {
            ["RequestMethod"] = request.Method.ToString(),
            ["RequestUri"] = request.RequestUri?.ToString(),
            ["Duration"] = duration.TotalMilliseconds,
            ["RequestHeaders"] = requestHeaders,
            ["RequestContent"] = requestContent,
            ["ErrorType"] = ex.GetType().FullName,
            ["ErrorMessage"] = ex.Message
        };

        _logger.LogError(ex, "{@HttpLog}", logData);
    }

    private async Task PrintLog(HttpRequestMessage request, HttpResponseMessage response, TimeSpan duration, CancellationToken cancellationToken)
    {
        var requestContent = await GetRequestContentAsync(request, cancellationToken);
        var responseContent = await GetResponseContentAsync(response, cancellationToken);
        var requestHeaders = GetHeaders(request.Headers, request.Content?.Headers);
        var responseHeaders = GetHeaders(response.Headers, response.Content?.Headers);

        Activity.Current?.SetTag("http.request.method", request.Method.ToString());
        Activity.Current?.SetTag("http.request.uri", request.RequestUri?.ToString());
        Activity.Current?.SetTag("http.response.status_code", (int)response.StatusCode);
        Activity.Current?.SetTag("http.request.duration_ms", duration.TotalMilliseconds);

        var logData = new Dictionary<string, object?>
        {
            ["RequestMethod"] = request.Method.ToString(),
            ["RequestUri"] = request.RequestUri?.ToString(),
            ["ResponseStatusCode"] = (int)response.StatusCode,
            ["Duration"] = duration.TotalMilliseconds,
            ["RequestHeaders"] = requestHeaders,
            ["RequestContent"] = requestContent,
            ["ResponseHeaders"] = responseHeaders,
            ["ResponseContent"] = responseContent
        };

        _logger.LogInformation("{@HttpLog}", logData);
    }

    private static Dictionary<string, string> GetHeaders(HttpHeaders headers,HttpContentHeaders? contentHeaders)
    {
        var headersDict = headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
        if (contentHeaders != null)
        {
            foreach (var header in contentHeaders)
            {
                headersDict[header.Key] = string.Join(", ", header.Value);
            }
        }

        return headersDict;
    }

    private static async Task<string> GetResponseContentAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.Content == null)
        {
            return string.Empty;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "[Binary Content]";
        }

        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return "[Read Content Failed]";
        }
    }

    private static async Task<string> GetRequestContentAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (request.Method == HttpMethod.Get || request.Content == null)
        {
            return string.Empty;
        }

        var reqContentType = request.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (reqContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
            reqContentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            reqContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            reqContentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            reqContentType.StartsWith("data-binary", StringComparison.OrdinalIgnoreCase))
        {
            return "[Binary Content]";
        }

        try
        {
            return await request.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return "[Read Content Failed]";
        }
    }
}
