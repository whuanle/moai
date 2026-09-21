using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库外部源类型.
/// </summary>
public enum WikiSourceType
{
    /// <summary>
    /// 飞书文档：绑定飞书应用后按知识空间节点拉取文档.
    /// </summary>
    [JsonPropertyName("feishuDoc")]
    FeishuDoc = 0,

    /// <summary>
    /// 网页爬虫：按起始 URL 抓取同站页面正文，支持抓取频率控制与定时后台爬取.
    /// </summary>
    [JsonPropertyName("crawler")]
    Crawler = 1,
}

/// <summary>
/// 外部源最近一次同步状态.
/// </summary>
public enum WikiSourceSyncStatus
{
    /// <summary>
    /// 尚未同步.
    /// </summary>
    [JsonPropertyName("none")]
    None = 0,

    /// <summary>
    /// 同步成功（全部文档处理成功）.
    /// </summary>
    [JsonPropertyName("success")]
    Success = 1,

    /// <summary>
    /// 同步失败（源级错误或部分文档失败）.
    /// </summary>
    [JsonPropertyName("failed")]
    Failed = 2,

    /// <summary>
    /// 正在同步.
    /// </summary>
    [JsonPropertyName("syncing")]
    Syncing = 3,
}

/// <summary>
/// 外部源文档同步状态.
/// </summary>
public enum WikiSourceDocumentStatus
{
    /// <summary>
    /// 待同步.
    /// </summary>
    [JsonPropertyName("pending")]
    Pending = 0,

    /// <summary>
    /// 已同步.
    /// </summary>
    [JsonPropertyName("synced")]
    Synced = 1,

    /// <summary>
    /// 同步失败.
    /// </summary>
    [JsonPropertyName("failed")]
    Failed = 2,
}

/// <summary>
/// 飞书文档外部源配置（存于 wiki_source.config）.
/// </summary>
public record WikiSourceFeishuConfig
{
    /// <summary>
    /// 飞书应用连接 id（feishu_app.id）.
    /// </summary>
    [JsonPropertyName("feishuAppId")]
    public Guid FeishuAppId { get; init; }

    /// <summary>
    /// 知识空间节点 token，从飞书文档链接中取.
    /// </summary>
    [JsonPropertyName("nodeToken")]
    public string NodeToken { get; init; } = string.Empty;

    /// <summary>
    /// 知识空间 id，首次同步后回填.
    /// </summary>
    [JsonPropertyName("spaceId")]
    public string? SpaceId { get; init; }

    /// <summary>
    /// 是否拉取该节点下的全部子文档.
    /// </summary>
    [JsonPropertyName("includeSubNodes")]
    public bool IncludeSubNodes { get; init; } = true;

    /// <summary>
    /// 子节点遍历最大深度，0 表示不限.
    /// </summary>
    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; init; }

    /// <summary>
    /// 单次同步文档数量上限，0 表示取默认值 <see cref="WikiSourceDefaults.DefaultMaxDocuments"/>.
    /// </summary>
    [JsonPropertyName("maxDocuments")]
    public int MaxDocuments { get; init; }
}

/// <summary>
/// 网页爬虫外部源配置（存于 wiki_source.config）.
/// 抓取范围采用「同站点 + 路径前缀限定」策略：仅抓取与起始地址同 host 且以 <see cref="PathPrefix"/> 开头的链接，
/// 并以 <see cref="MaxDepth"/>、<see cref="MaxPages"/> 双重兜底，避免抓取失控.
/// </summary>
public record WikiSourceCrawlerConfig
{
    /// <summary>
    /// 起始 URL（爬取入口）.
    /// </summary>
    [JsonPropertyName("startUrl")]
    public string StartUrl { get; init; } = string.Empty;

    /// <summary>
    /// 抓取路径前缀限定，仅抓取以该前缀开头的地址；空串表示限定为起始 URL 所在目录.
    /// </summary>
    [JsonPropertyName("pathPrefix")]
    public string PathPrefix { get; init; } = string.Empty;

    /// <summary>
    /// 链接遍历最大深度，1 表示仅抓起始页，0 表示取默认值.
    /// </summary>
    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; init; }

    /// <summary>
    /// 单轮爬取页面数量上限，0 表示取默认值.
    /// </summary>
    [JsonPropertyName("maxPages")]
    public int MaxPages { get; init; }

    /// <summary>
    /// 相邻两次请求的最小间隔秒数，用于避免把目标站点抓崩；0 表示取默认值.
    /// </summary>
    [JsonPropertyName("requestIntervalSeconds")]
    public int RequestIntervalSeconds { get; init; }

    /// <summary>
    /// 单次请求超时秒数，0 表示取默认值.
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; init; }

    /// <summary>
    /// 请求 UserAgent，空串表示取默认值.
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; init; }

    /// <summary>
    /// 正文选择器（CSS），空串表示抽取整个 body.
    /// </summary>
    [JsonPropertyName("contentSelector")]
    public string? ContentSelector { get; init; }

    /// <summary>
    /// 是否覆盖已抓取且内容变化的页面；false 表示已存在的页面直接跳过，不重复抓取.
    /// </summary>
    [JsonPropertyName("isOverwriteExisting")]
    public bool IsOverwriteExisting { get; init; } = true;
}

/// <summary>
/// 外部源配置 JSON 序列化助手：camelCase 属性 + camelCase 枚举字符串，与全局 API 序列化约定一致.
/// </summary>
public static class WikiSourceConfigJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    /// <summary>
    /// 序列化为存储用 JSON.
    /// </summary>
    /// <param name="config">飞书文档源配置.</param>
    /// <returns>JSON 字符串.</returns>
    public static string SerializeFeishu(WikiSourceFeishuConfig config)
    {
        return JsonSerializer.Serialize(config, Options);
    }

    /// <summary>
    /// 从存储 JSON 反序列化飞书文档源配置；空串或非法 JSON 返回 null.
    /// </summary>
    /// <param name="json">存储 JSON.</param>
    /// <returns>配置，失败时为 null.</returns>
    public static WikiSourceFeishuConfig? DeserializeFeishu(string? json)
    {
        return Deserialize<WikiSourceFeishuConfig>(json);
    }

    /// <summary>
    /// 序列化为存储用 JSON.
    /// </summary>
    /// <param name="config">网页爬虫源配置.</param>
    /// <returns>JSON 字符串.</returns>
    public static string SerializeCrawler(WikiSourceCrawlerConfig config)
    {
        return JsonSerializer.Serialize(config, Options);
    }

    /// <summary>
    /// 从存储 JSON 反序列化网页爬虫源配置；空串或非法 JSON 返回 null.
    /// </summary>
    /// <param name="json">存储 JSON.</param>
    /// <returns>配置，失败时为 null.</returns>
    public static WikiSourceCrawlerConfig? DeserializeCrawler(string? json)
    {
        return Deserialize<WikiSourceCrawlerConfig>(json);
    }

    private static T? Deserialize<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// 外部源默认值与校验常量.
/// </summary>
public static class WikiSourceDefaults
{
    /// <summary>
    /// 单次同步文档数量默认上限.
    /// </summary>
    public const int DefaultMaxDocuments = 200;

    /// <summary>
    /// 单次同步文档数量硬上限（配置可填的最大值）.
    /// </summary>
    public const int MaxDocumentsLimit = 1000;

    /// <summary>
    /// 子节点遍历深度上限.
    /// </summary>
    public const int MaxDepthLimit = 10;

    /// <summary>
    /// 爬虫单轮抓取页面数默认上限.
    /// </summary>
    public const int DefaultCrawlerMaxPages = 200;

    /// <summary>
    /// 爬虫单轮抓取页面数硬上限（配置可填的最大值）.
    /// </summary>
    public const int CrawlerMaxPagesLimit = 2000;

    /// <summary>
    /// 爬虫链接遍历深度硬上限.
    /// </summary>
    public const int CrawlerMaxDepthLimit = 10;

    /// <summary>
    /// 爬虫相邻请求最小间隔默认秒数（对目标站点友好，避免抓崩）.
    /// </summary>
    public const int DefaultCrawlerRequestIntervalSeconds = 1;

    /// <summary>
    /// 爬虫相邻请求间隔硬下限（秒），小于该值一律按该值执行.
    /// </summary>
    public const int MinCrawlerRequestIntervalSeconds = 1;

    /// <summary>
    /// 爬虫相邻请求间隔硬上限（秒）.
    /// </summary>
    public const int MaxCrawlerRequestIntervalSeconds = 3600;

    /// <summary>
    /// 爬虫单次请求超时默认秒数.
    /// </summary>
    public const int DefaultCrawlerTimeoutSeconds = 30;

    /// <summary>
    /// 爬虫单次请求超时上限（秒）.
    /// </summary>
    public const int MaxCrawlerTimeoutSeconds = 300;

    /// <summary>
    /// 爬虫请求 UserAgent 默认值：显式声明机器人身份，便于站点管理员识别与封禁.
    /// </summary>
    public const string DefaultCrawlerUserAgent = "MoAI-Crawler/1.0 (+https://moai.dev/bot)";

    /// <summary>
    /// 爬虫正文选择器最大长度.
    /// </summary>
    public const int CrawlerSelectorMaxLength = 255;

    /// <summary>
    /// 爬虫起始 URL 最大长度.
    /// </summary>
    public const int CrawlerUrlMaxLength = 1000;

    /// <summary>
    /// 爬虫限速：同一站点连续失败达到该次数即中止本轮抓取（熔断），避免对故障站点持续施压.
    /// </summary>
    public const int CrawlerMaxConsecutiveFailures = 5;

    /// <summary>
    /// 解析爬虫的请求间隔秒数：未配置或小于下限时取默认值，并夹取到允许区间.
    /// </summary>
    /// <param name="seconds">配置值.</param>
    /// <returns>实际使用的间隔秒数.</returns>
    public static int ResolveCrawlerRequestInterval(int seconds)
    {
        if (seconds <= 0)
        {
            return DefaultCrawlerRequestIntervalSeconds;
        }

        return Math.Clamp(seconds, MinCrawlerRequestIntervalSeconds, MaxCrawlerRequestIntervalSeconds);
    }

    /// <summary>
    /// 解析爬虫超时秒数：未配置时取默认值，并夹取到允许区间.
    /// </summary>
    /// <param name="seconds">配置值.</param>
    /// <returns>实际使用的超时秒数.</returns>
    public static int ResolveCrawlerTimeout(int seconds)
    {
        if (seconds <= 0)
        {
            return DefaultCrawlerTimeoutSeconds;
        }

        return Math.Clamp(seconds, 5, MaxCrawlerTimeoutSeconds);
    }

    /// <summary>
    /// 校验爬虫起始 URL：必须为 http/https 绝对地址.
    /// </summary>
    /// <param name="url">起始 URL.</param>
    /// <returns>是否合法.</returns>
    public static bool IsValidCrawlerUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > CrawlerUrlMaxLength)
        {
            return false;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// cron 表达式最大长度.
    /// </summary>
    public const int CronMaxLength = 64;

    /// <summary>
    /// 外部源定时同步在 Hangfire 中的任务 key 前缀.
    /// </summary>
    public const string SyncJobKeyPrefix = "wiki-source-sync:";

    /// <summary>
    /// 构造外部源定时同步的任务 key.
    /// </summary>
    /// <param name="sourceId">外部源 id.</param>
    /// <returns>任务 key.</returns>
    public static string BuildSyncJobKey(Guid sourceId)
    {
        return SyncJobKeyPrefix + sourceId.ToString("D");
    }

    /// <summary>
    /// 校验 cron 表达式形态（5 或 6 段，仅允许 cron 常用字符），不校验语义.
    /// </summary>
    /// <param name="cron">cron 表达式.</param>
    /// <returns>是否合法.</returns>
    public static bool IsValidCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return false;
        }

        if (cron.Length > CronMaxLength)
        {
            return false;
        }

        var fields = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is not (5 or 6))
        {
            return false;
        }

        foreach (var field in fields)
        {
            foreach (var c in field)
            {
                if (!char.IsDigit(c) && c is not ('*' or '/' or '-' or ',' or '?' or 'L' or 'W' or '#'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
