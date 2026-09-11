using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Helpers;
using MoAI.AIPlugin.Static.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Put;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 网页内容抓取（静态插件）：抓取指定网页，可选择返回纯文本或原始 HTML.
/// </summary>
[AiPlugin(key: "static_web_content_fetch", Name = "网页内容抓取", Description = "获取指定网页链接的内容，默认提取纯文本")]
public class WebContentFetchPlugin : IStaticPluginRuntime<WebContentFetchRequest, WebContentFetchResponse>
{
    /// <summary>
    /// 模拟搜索引擎爬虫的 User-Agent，提升对静态页面的抓取成功率.
    /// </summary>
    private const string GoogleBotUserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    /// <summary>
    /// 抓取超时时间.
    /// </summary>
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private readonly IPutClient _putClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebContentFetchPlugin"/> class.
    /// </summary>
    /// <param name="putClient">外部 HTTP 客户端（复用统一的外部请求日志与遥测）.</param>
    public WebContentFetchPlugin(IPutClient putClient)
    {
        _putClient = putClient;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Url": "https://example.com",   // 目标网页链接
              "ExtractText": true             // 是否提取纯文本；为 false 时返回原始 HTML
            }
            """;
    }

    /// <inheritdoc/>
    public async Task<WebContentFetchResponse> RunAsync(WebContentFetchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(400, "URL 必须为合法的 http/https 地址");
        }

        var html = await FetchHtmlAsync(uri, cancellationToken).ConfigureAwait(false);

        if (!request.ExtractText)
        {
            return new WebContentFetchResponse { Content = html };
        }

        try
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html)).WaitAsync(cancellationToken).ConfigureAwait(false);
            return new WebContentFetchResponse { Content = AngleSharpHelper.ExtractTextContent(document) };
        }
#pragma warning disable CA1031 // 解析失败统一归一化为业务异常，便于在运行抽屉定位
        catch (Exception ex)
        {
            throw new BusinessException(400, $"解析网页内容失败: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// 下载网页 HTML（仅静态抓取，不执行页面脚本）.
    /// </summary>
    /// <param name="uri">目标网页地址.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>网页 HTML 文本.</returns>
    private async Task<string> FetchHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(FetchTimeout);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            httpRequest.Headers.UserAgent.ParseAdd(GoogleBotUserAgent);

            using var response = await _putClient.Client
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 上层主动取消，直接向上抛，让执行引擎归一化为取消结果.
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new BusinessException(400, "抓取网页内容超时（10 秒）");
        }
        catch (HttpRequestException ex)
        {
            throw new BusinessException(400, $"抓取网页内容失败: {ex.Message}");
        }
    }
}
