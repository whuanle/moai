using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.HotBoard;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 新闻热榜（动态插件）：按来源并发拉取多平台热榜/热搜榜单（36氪、知乎、微博、稀土掘金、今日头条、
/// 百度、B站、抖音、IT之家、虎扑、虎嗅、澎湃、极客公园、腾讯新闻、网易新闻、新浪新闻），
/// 合并返回排名、标题、链接、摘要、封面与热度值；单个来源失败不影响其余来源.
/// </summary>
[AiPlugin(key: "news_hot_board", Name = "新闻热榜", Description = "获取多平台新闻热榜/热搜榜单，支持 36氪、知乎、微博、掘金、头条、百度、B站、抖音、IT之家、虎扑、虎嗅、澎湃、极客公园、腾讯新闻、网易新闻、新浪新闻，返回排名、标题、链接、摘要、封面与热度值")]
public class HotBoardPlugin : IDynamicPluginRuntime<HotBoardRequest, HotBoardResponse, HotBoardConfig>
{
    /// <summary>
    /// 每来源条数下限.
    /// </summary>
    public const int MinLimit = 1;

    /// <summary>
    /// 每来源条数上限.
    /// </summary>
    public const int MaxLimit = 50;

    /// <summary>
    /// 命名 HTTP 客户端（模块注册：超时 + 外部请求日志拦截器）.
    /// </summary>
    public const string HttpClientName = "hot_board";

    private readonly IHttpClientFactory _httpClientFactory;
    private HotBoardConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HotBoardPlugin"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂（由 DI 作用域解析）.</param>
    public HotBoardPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Sources": ["zhihu", "weibo", "36kr"] // 热榜来源，可多选：36kr、zhihu、weibo、juejin、toutiao、baidu、bilibili、douyin、ithome、hupu、huxiu、thepaper、geekpark、qq-news、netease-news、sina-news（也接受中文名）
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "Limit": 20,    // 每个来源返回条数上限 1-50，默认 20
              "Gateway": ""   // 调试网关（可选）：设置后所有平台请求改写为 {Gateway}/https/{域名}/{路径}，留空直连官方
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(HotBoardConfig config)
    {
        var gateway = config.Gateway?.Trim();
        if (!string.IsNullOrEmpty(gateway)
            && (!Uri.TryCreate(gateway, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return Task.FromResult<string?>("网关地址 Gateway 必须是合法的 http:// 或 https:// URL，留空直连各平台官方地址");
        }

        _config = new HotBoardConfig
        {
            Limit = Math.Clamp(config.Limit, MinLimit, MaxLimit),
            Gateway = string.IsNullOrEmpty(gateway) ? null : gateway,
        };
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<HotBoardResponse> RunAsync(HotBoardRequest request, CancellationToken cancellationToken)
    {
        if (!HotBoardCatalog.TryNormalize(request.Sources, out var ids, out var unknown))
        {
            throw new BusinessException(400, $"热榜来源 Sources 不能为空，可选值：{HotBoardCatalog.DescribeIds()}");
        }

        if (unknown.Count > 0)
        {
            throw new BusinessException(400, $"未知热榜来源：{string.Join('、', unknown)}；可选值：{HotBoardCatalog.DescribeIds()}");
        }

        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var gateway = _config.Gateway;
        var results = await Task.WhenAll(ids
            .Select(id => FetchChannelAsync(id, httpClient, gateway, cancellationToken)))
            .ConfigureAwait(false);

        var news = new List<HotBoardNewsItem>();
        var errors = new List<string>();
        foreach (var result in results)
        {
            if (result.Error is not null)
            {
                errors.Add(result.Error);
                continue;
            }

            var displayName = HotBoardCatalog.DisplayNameOf(result.Id);
            var rank = 0;
            foreach (var entry in result.Items.Take(_config.Limit))
            {
                news.Add(new HotBoardNewsItem
                {
                    Source = result.Id,
                    SourceName = displayName,
                    Rank = ++rank,
                    Title = entry.Title,
                    Url = entry.Url ?? string.Empty,
                    Desc = entry.Desc,
                    Cover = entry.Cover,
                    HotValue = entry.HotValue,
                });
            }
        }

        if (news.Count == 0)
        {
            throw new BusinessException(502, errors.Count > 0
                ? $"全部来源抓取失败：{string.Join("；", errors)}"
                : "所有来源均未返回榜单条目");
        }

        return new HotBoardResponse { NewsList = news, Errors = errors };
    }

    /// <summary>
    /// 抓取单个渠道并把异常归一为错误消息（单渠道失败不拖垮整轮）.
    /// </summary>
    /// <param name="id">渠道 id.</param>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回渠道抓取结果.</returns>
    private static async Task<ChannelResult> FetchChannelAsync(string id, HttpClient httpClient, string? gateway, CancellationToken cancellationToken)
    {
        try
        {
            var channel = HotBoardCatalog.Create(id, httpClient, gateway);
            var items = await channel.FetchAsync(cancellationToken).ConfigureAwait(false);
            if (items.Count == 0)
            {
                return new ChannelResult(id, Error: $"{HotBoardCatalog.DisplayNameOf(id)} 未返回任何条目（可能被风控或榜单结构变化）");
            }

            return new ChannelResult(id, Items: items);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BusinessException ex)
        {
            return new ChannelResult(id, Error: $"{HotBoardCatalog.DisplayNameOf(id)}：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new ChannelResult(id, Error: $"{HotBoardCatalog.DisplayNameOf(id)} 抓取失败：{ex.Message}");
        }
    }

    private sealed record ChannelResult(string Id, IReadOnlyList<HotBoardEntry>? Items = null, string? Error = null);
}
