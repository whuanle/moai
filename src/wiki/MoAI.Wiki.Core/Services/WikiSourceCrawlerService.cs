#pragma warning disable CA1031 // 爬虫需对单个页面的任意异常做隔离，避免整轮抓取中断
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 外部源网页爬虫：按起始 URL 以广度优先抓取「同站点 + 路径前缀限定」范围内的页面，
/// 提取正文转 Markdown 后交由 <see cref="WikiSourceContentWriter"/> 落库并触发工作流。
/// <para>
/// 抓取友好性（避免把一个网站抓崩）：
/// ① 全串行抓取，同一外部源的两次请求之间强制等待 <c>requestIntervalSeconds</c> 秒；
/// ② 单轮页数与遍历深度双重上限；
/// ③ 连续失败达到阈值即熔断中止本轮；
/// ④ 显式声明 UserAgent 并在遇 429/503 时按 Retry-After 退避。
/// </para>
/// </summary>
[InjectOnScoped]
public class WikiSourceCrawlerService
{
    /// <summary>
    /// 搜索引擎式爬虫默认不抓取的协议前缀.
    /// </summary>
    private static readonly string[] SkipSchemes = { "mailto:", "javascript:", "tel:", "data:", "ftp:", "file:" };

    private readonly DatabaseContext _databaseContext;
    private readonly WikiSourceContentWriter _contentWriter;
    private readonly ILogger<WikiSourceCrawlerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSourceCrawlerService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="contentWriter">外部源文档落库与工作流触发.</param>
    /// <param name="logger">日志.</param>
    public WikiSourceCrawlerService(
        DatabaseContext databaseContext,
        WikiSourceContentWriter contentWriter,
        ILogger<WikiSourceCrawlerService> logger)
    {
        _databaseContext = databaseContext;
        _contentWriter = contentWriter;
        _logger = logger;
    }

    /// <summary>
    /// 执行一次完整的爬取同步：广度优先遍历站点、增量比对内容、落库并触发工作流.
    /// </summary>
    /// <param name="source">外部源实体.</param>
    /// <param name="force">是否忽略内容哈希强制重新处理全部页面.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>同步统计.</returns>
    public async Task<CrawlerSyncResult> CrawlAsync(WikiSourceEntity source, bool force, CancellationToken cancellationToken)
    {
        var config = WikiSourceConfigJson.DeserializeCrawler(source.Config);
        if (config == null || !WikiSourceDefaults.IsValidCrawlerUrl(config.StartUrl))
        {
            throw new BusinessException("外部源配置不完整，请检查起始 URL.") { StatusCode = 400 };
        }

        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == source.WikiId && x.IsDeleted == 0, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var startUri = new Uri(config.StartUrl.Trim());
        var pathPrefix = ResolvePathPrefix(startUri, config.PathPrefix);
        var maxDepth = config.MaxDepth > 0
            ? Math.Min(config.MaxDepth, WikiSourceDefaults.CrawlerMaxDepthLimit)
            : WikiSourceDefaults.CrawlerMaxDepthLimit;
        var maxPages = config.MaxPages > 0
            ? Math.Min(config.MaxPages, WikiSourceDefaults.CrawlerMaxPagesLimit)
            : WikiSourceDefaults.DefaultCrawlerMaxPages;
        var interval = TimeSpan.FromSeconds(WikiSourceDefaults.ResolveCrawlerRequestInterval(config.RequestIntervalSeconds));
        var timeout = TimeSpan.FromSeconds(WikiSourceDefaults.ResolveCrawlerTimeout(config.TimeoutSeconds));
        var userAgent = string.IsNullOrWhiteSpace(config.UserAgent)
            ? WikiSourceDefaults.DefaultCrawlerUserAgent
            : config.UserAgent!.Trim();

        var result = new CrawlerSyncResult();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Url, string Path, int Depth)>();
        var normalizedStart = NormalizeUrl(startUri);
        queue.Enqueue((normalizedStart, NormalizeTitlePath(string.Empty, startUri), 1));
        visited.Add(normalizedStart);

        var consecutiveFailures = 0;
        var lastRequestTime = DateTimeOffset.MinValue;
        var breakerTripped = false;

        using var httpClient = CreateHttpClient(userAgent, timeout);
        var parser = new HtmlParser();

        // 展开当前页面的子链接：正常、内容无变化两种结果都要展开，否则新增的下级页面发现不到
        void EnqueueChildren(string html, string baseUrl, string parentPath, int depth)
        {
            if (depth >= maxDepth)
            {
                return;
            }

            foreach (var link in ExtractLinks(html, parser, baseUrl, startUri, pathPrefix))
            {
                if (result.Total + queue.Count >= maxPages)
                {
                    break;
                }

                if (visited.Add(link.Url))
                {
                    queue.Enqueue((link.Url, NormalizeTitlePath(parentPath, new Uri(link.Url)), depth + 1));
                }
            }
        }

        while (queue.Count > 0 && result.Total < maxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (url, path, depth) = queue.Dequeue();

            // 保底限速：同一源两次请求之间至少间隔 interval
            var wait = interval - (DateTimeOffset.UtcNow - lastRequestTime);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            lastRequestTime = DateTimeOffset.UtcNow;

            try
            {
                var (html, finalUrl, retryAfter) = await FetchAsync(httpClient, url, cancellationToken);
                consecutiveFailures = 0;

                if (retryAfter.HasValue)
                {
                    // 目标站点显式要求降速：按 Retry-After 让路，不视为失败
                    _logger.LogInformation(
                        "爬虫收到限流响应，按 Retry-After 退避. SourceId={SourceId}, Url={Url}, RetryAfter={RetryAfter}",
                        source.Id,
                        url,
                        retryAfter.Value);
                    await Task.Delay(retryAfter.Value, cancellationToken);
                }

                result.Total++;
                var title = ExtractTitle(html, finalUrl);
                var content = ExtractContent(html, parser, config.ContentSelector, finalUrl);

                if (string.IsNullOrWhiteSpace(content))
                {
                    result.Skipped++;
                    result.Items.Add(new CrawlerPageResult
                    {
                        Url = finalUrl,
                        Title = title,
                        Result = "skipped",
                        Message = "页面正文为空，已跳过",
                    });
                    EnqueueChildren(html, finalUrl, path, depth);
                    continue;
                }

                var hash = WikiSourceContentWriter.ComputeSha256(content);
                var mapping = await _databaseContext.WikiSourceDocuments
                    .FirstOrDefaultAsync(x => x.SourceId == source.Id && x.ExternalKey == finalUrl, cancellationToken);

                if (mapping != null && !force && string.Equals(mapping.ContentHash, hash, StringComparison.Ordinal))
                {
                    result.Unchanged++;
                    mapping.LastSyncTime = DateTimeOffset.UtcNow;
                    mapping.LastError = string.Empty;
                    await _databaseContext.SaveChangesAsync(cancellationToken);
                    result.Items.Add(new CrawlerPageResult
                    {
                        Url = finalUrl,
                        Title = title,
                        DocumentId = mapping.DocumentId,
                        Result = "unchanged",
                        Message = "内容无变化",
                    });

                    // 内容虽未变化，仍需展开子链接：否则新增的下级页面在本轮永远发现不了
                    EnqueueChildren(html, finalUrl, path, depth);
                    continue;
                }

                // 已存在且不允许覆盖：跳过本页写入，但仍展开子链接以免漏掉新增页面
                if (mapping != null && !config.IsOverwriteExisting && !force)
                {
                    result.Skipped++;
                    result.Items.Add(new CrawlerPageResult
                    {
                        Url = finalUrl,
                        Title = title,
                        DocumentId = mapping.DocumentId,
                        Result = "skipped",
                        Message = "页面已存在且未开启覆盖",
                    });
                    EnqueueChildren(html, finalUrl, path, depth);
                    continue;
                }

                var isNew = mapping == null;
                var write = await _contentWriter.WriteAsync(
                    wiki,
                    source,
                    new WikiSourceContentItem
                    {
                        ExternalKey = finalUrl,
                        ExternalDocToken = finalUrl,
                        Title = title,
                        Path = path,
                        Content = content,
                        ContentHash = hash,
                        Revision = string.Empty,
                    },
                    mapping,
                    cancellationToken);

                if (isNew)
                {
                    result.Created++;
                }
                else
                {
                    result.Updated++;
                }

                if (write.TaskId.HasValue)
                {
                    result.WorkflowTriggered++;
                }

                result.Items.Add(new CrawlerPageResult
                {
                    Url = finalUrl,
                    Title = title,
                    DocumentId = write.DocumentId,
                    Result = isNew ? "created" : "updated",
                    Message = write.Message,
                });

                // 页面处理成功后展开子链接，保证深度与页数上限始终有效
                EnqueueChildren(html, finalUrl, path, depth);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                result.Failed++;
                _logger.LogWarning(ex, "爬虫抓取页面失败. SourceId={SourceId}, Url={Url}", source.Id, url);
                result.Items.Add(new CrawlerPageResult
                {
                    Url = url,
                    Title = url,
                    Result = "failed",
                    Message = WikiSourceContentWriter.Truncate(ex.Message, 500),
                });

                if (consecutiveFailures >= WikiSourceDefaults.CrawlerMaxConsecutiveFailures)
                {
                    breakerTripped = true;
                    _logger.LogWarning(
                        "爬虫连续失败 {Count} 次，本轮抓取提前中止以保护目标站点. SourceId={SourceId}",
                        consecutiveFailures,
                        source.Id);
                    break;
                }
            }
        }

        result.BreakerTripped = breakerTripped;
        result.Truncated = queue.Count > 0;
        return result;
    }

    /// <summary>
    /// 解析实际生效的路径前缀：显式配置优先；未配置时限定为起始 URL 所在目录.
    /// </summary>
    private static string ResolvePathPrefix(Uri startUri, string? configuredPrefix)
    {
        if (!string.IsNullOrWhiteSpace(configuredPrefix))
        {
            var prefix = configuredPrefix.Trim();
            if (Uri.TryCreate(prefix, UriKind.Absolute, out var absolute))
            {
                return absolute.GetLeftPart(UriPartial.Path).TrimEnd('/');
            }

            // 相对前缀（如 /docs）直接与站点根拼接
            var relative = prefix.StartsWith('/') ? prefix : "/" + prefix;
            return (startUri.GetLeftPart(UriPartial.Authority) + relative).TrimEnd('/');
        }

        var directory = startUri.GetLeftPart(UriPartial.Path);
        var lastSlash = directory.LastIndexOf('/');
        return lastSlash > startUri.GetLeftPart(UriPartial.Authority).Length
            ? directory[..lastSlash]
            : startUri.GetLeftPart(UriPartial.Authority);
    }

    private static HttpClient CreateHttpClient(string userAgent, TimeSpan timeout)
    {
        // handler 所有权转移给 HttpClient（disposeHandler: true），由调用方的 using 负责释放
#pragma warning disable CA2000
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            CheckCertificateRevocationList = true,
        };
#pragma warning restore CA2000

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout,
        };

        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
        return client;
    }

    /// <summary>
    /// 抓取单个页面：返回 HTML、最终地址（跟随重定向后）与站点要求的退避时长.
    /// </summary>
    private static async Task<(string Html, string FinalUrl, TimeSpan? RetryAfter)> FetchAsync(
        HttpClient httpClient,
        string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        if ((int)response.StatusCode is 429 or 503)
        {
            var retryAfter = ParseRetryAfter(response);
            return (string.Empty, url, retryAfter ?? TimeSpan.FromSeconds(5));
        }

        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (mediaType.Length > 0
            && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException($"页面不是 HTML 类型（{mediaType}），已跳过.") { StatusCode = 400 };
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
        return (html, finalUrl, null);
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
        {
            return null;
        }

        if (retryAfter.Delta.HasValue)
        {
            return retryAfter.Delta;
        }

        if (retryAfter.Date.HasValue)
        {
            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static string ExtractTitle(string html, string url)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var title = document.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            return WikiSourceContentWriter.Truncate(title, 200);
        }

        var h1 = document.QuerySelector("h1")?.TextContent?.Trim();
        if (!string.IsNullOrWhiteSpace(h1))
        {
            return WikiSourceContentWriter.Truncate(h1, 200);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimEnd('/');
            var last = path.LastIndexOf('/');
            var name = last >= 0 && last < path.Length - 1 ? path[(last + 1)..] : uri.Host;
            return WikiSourceContentWriter.Truncate(name, 200);
        }

        return url;
    }

    /// <summary>
    /// 提取页面正文并转为 Markdown 文本：按选择器裁剪，未配置选择器时移除脚本/样式等噪声节点.
    /// </summary>
    private static string ExtractContent(string html, HtmlParser parser, string? selector, string url)
    {
        var document = parser.ParseDocument(html);

        // 先剔除明显噪声，减少送入切割的无关内容
        foreach (var node in document.QuerySelectorAll("script,style,noscript,svg,iframe,header,footer,nav"))
        {
            node.Remove();
        }

        IElement? root = null;
        if (!string.IsNullOrWhiteSpace(selector))
        {
            root = document.QuerySelector(selector.Trim());
        }

        root ??= document.Body ?? document.DocumentElement;
        if (root == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var title = document.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.Append("# ").AppendLine(title).AppendLine();
        }

        builder.Append("> 来源：").AppendLine(url).AppendLine();

        var text = ExtractText(root, builder);
        return text.ToString().Trim();
    }

    /// <summary>
    /// 递归把 DOM 转为轻量 Markdown（保留标题/段落/列表/代码/引用与链接），避免引入额外渲染依赖.
    /// </summary>
    private static StringBuilder ExtractText(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text)
            {
                var content = CollapseWhitespace(text.Data);
                if (content.Length > 0)
                {
                    builder.Append(content).Append(' ');
                }

                continue;
            }

            if (child is not IElement element)
            {
                continue;
            }

            var tag = element.LocalName;
            switch (tag)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    var level = tag[1] - '0';
                    builder.AppendLine().Append(new string('#', level)).Append(' ');
                    ExtractText(element, builder);
                    builder.AppendLine().AppendLine();
                    break;
                case "p":
                case "div":
                case "section":
                case "article":
                case "main":
                case "br":
                    builder.AppendLine().AppendLine();
                    ExtractText(element, builder);
                    builder.AppendLine().AppendLine();
                    break;
                case "li":
                    builder.AppendLine().Append("- ");
                    ExtractText(element, builder);
                    builder.AppendLine();
                    break;
                case "pre":
                case "code":
                    builder.AppendLine().Append("```").AppendLine();
                    builder.Append(element.TextContent.Trim()).AppendLine();
                    builder.AppendLine().Append("```").AppendLine();
                    break;
                case "blockquote":
                    builder.AppendLine().Append("> ");
                    ExtractText(element, builder);
                    builder.AppendLine();
                    break;
                case "a":
                    var href = element.GetAttribute("href");
                    var label = CollapseWhitespace(element.TextContent);
                    if (!string.IsNullOrWhiteSpace(href) && !href.StartsWith('#'))
                    {
                        builder.Append('[').Append(label).Append("](").Append(href).Append(") ");
                    }
                    else if (label.Length > 0)
                    {
                        builder.Append(label).Append(' ');
                    }

                    break;
                case "td":
                case "th":
                    builder.Append('|').Append(' ');
                    ExtractText(element, builder);
                    break;
                case "tr":
                    builder.AppendLine();
                    ExtractText(element, builder);
                    builder.Append('|');
                    builder.AppendLine();
                    break;
                default:
                    ExtractText(element, builder);
                    break;
            }
        }

        return builder;
    }

    /// <summary>
    /// 解析页面链接：仅保留同站点、落在路径前缀内、且未访问过的 http/https 绝对地址.
    /// </summary>
    private static IEnumerable<(string Url, string Path)> ExtractLinks(
        string html,
        HtmlParser parser,
        string baseUrl,
        Uri startUri,
        string pathPrefix)
    {
        var document = parser.ParseDocument(html);
        var baseUri = new Uri(baseUrl);

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var raw = href.Trim();

            // 跳过非页面协议与纯锚点
            if (raw.StartsWith('#') || SkipSchemes.Any(s => raw.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, raw, out var uri))
            {
                continue;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            // 仅同站点
            if (!string.Equals(uri.Host, startUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var canonical = NormalizeUrl(uri);

            // 仅路径前缀范围内
            if (!IsUnderPrefix(canonical, pathPrefix))
            {
                continue;
            }

            yield return (canonical, NormalizeTitlePath(string.Empty, uri));
        }
    }

    private static bool IsUnderPrefix(string url, string prefix)
    {
        return url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || url.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 归一化 URL：去掉 fragment、统一小写 host、剔除常见跟踪参数，用于跨轮次稳定比对.
    /// </summary>
    private static string NormalizeUrl(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Host = uri.Host,
        };

        if (builder.Path.Length == 0)
        {
            builder.Path = "/";
        }

        // 剔除常见跟踪参数，避免同一页面因参数不同被重复抓取
        var keep = System.Web.HttpUtility.ParseQueryString(builder.Query)
            .AllKeys
            .Where(k => k != null && !k.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && k != "from" && k != "spm")
            .Select(k => $"{k}={System.Web.HttpUtility.ParseQueryString(builder.Query)[k]}")
            .ToList();

        builder.Query = string.Join("&", keep);
        return builder.Uri.ToString();
    }

    private static string NormalizeTitlePath(string parentPath, Uri uri)
    {
        var segment = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(segment))
        {
            segment = uri.Host;
        }

        var lastSlash = segment.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < segment.Length - 1)
        {
            segment = segment[(lastSlash + 1)..];
        }

        return string.IsNullOrWhiteSpace(parentPath) ? segment : $"{parentPath}/{segment}";
    }

    private static string CollapseWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            builder.Append(c);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }
}
