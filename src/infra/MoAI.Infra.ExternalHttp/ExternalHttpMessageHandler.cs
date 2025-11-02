
#pragma warning disable SA1118 // Parameter should not span multiple lines
using Microsoft.Extensions.Logging;

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
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            await PrintLog(request, response, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await PrintExceptionLog(request, ex);
            throw;
        }
    }

    private async Task PrintExceptionLog(HttpRequestMessage request, Exception ex)
    {
        string requestContent = string.Empty;
        string requestHeaders = string.Join(Environment.NewLine, request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
        if (request.Content != null)
        {
            requestHeaders += Environment.NewLine + string.Join(Environment.NewLine, request.Content.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
        }

        if (request.Method != HttpMethod.Get && request.Content != null)
        {
            var reqContentType = request.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!reqContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) &&
                !reqContentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
                !reqContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !reqContentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                requestContent = await request.Content.ReadAsStringAsync();
            }
        }

        _logger.LogError(
            ex,
            """
            Request Method: {RequestMethod}
            Request Uri: {RequestUri}
            Request Headers:
            {RequestHeaders}
            Request Content:
            {RequestContent}
        """,
            request.Method,
            request.RequestUri,
            requestHeaders,
            requestContent);
    }

    private async Task PrintLog(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string requestContent = string.Empty;
        string requestHeaders = string.Join(Environment.NewLine, request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
        if (request.Content != null)
        {
            requestHeaders += Environment.NewLine + string.Join(Environment.NewLine, request.Content.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
        }

        if (request.Method != HttpMethod.Get && request.Content != null)
        {
            var reqContentType = request.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!reqContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) &&
                !reqContentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
                !reqContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !reqContentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                requestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }
        }

        string responseContent = string.Empty;
        string responseHeaders = string.Join(Environment.NewLine, response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
        if (response.Content != null)
        {
            responseHeaders += Environment.NewLine + string.Join(Environment.NewLine, response.Content.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
                !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }

        _logger.LogInformation(
    """
    Request Method: {RequestMethod}
    Request Uri: {RequestUri}
    Request Headers:
    {RequestHeaders}
    Request Content:
    {RequestContent}
    Response Status Code: {ResponseStatusCode}
    Response Headers:
    {ResponseHeaders}
    Response Content:
    {ResponseContent}
    """,
    request.Method,
    request.RequestUri,
    requestHeaders,
    requestContent,
    response.StatusCode,
    responseHeaders,
    responseContent);
    }
}
