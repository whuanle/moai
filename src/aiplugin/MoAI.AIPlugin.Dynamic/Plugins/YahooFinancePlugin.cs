using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.YahooFinance;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 雅虎财经（动态插件）：调用雅虎财经公开数据接口，让 AI 查询股票报价、搜索财经新闻与解析股票代码.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="YahooFinanceQueryRequest.Mode"/>）：quote（批量报价，逐代码调 v8 chart，单代码失败不拖垮整批）/
/// news（v1 search 搜新闻）/search（v1 search 解析代码候选）。数据源地址来自实例配置（官方/镜像/桩服务均可），
/// 无需 API Key；上游对无浏览器 User-Agent 的请求常直接拒绝，故客户端统一携带浏览器 UA。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件；客户端为 transient，BaseAddress/超时按实例配置每次重设。
/// </remarks>
[AiPlugin(
    key: "yahoo_finance",
    Name = "雅虎财经",
    Description = "查询雅虎财经数据：Mode 支持 quote（批量股票报价：最新价/涨跌/日内高低/成交量/52 周高低，可选近期走势）/news（财经新闻搜索）/search（把公司名或别名解析成股票代码）；先用 search 确定代码，再 quote 查报价、news 查相关新闻")]
public class YahooFinancePlugin : IDynamicPluginRuntime<YahooFinanceQueryRequest, YahooFinanceQueryResponse, YahooFinanceQueryConfig>
{
    private const string OfficialBaseUrl = "https://query1.finance.yahoo.com/";

    /// <summary>走势序列最多保留的采样点数.</summary>
    private const int MaxHistoryPoints = 30;

    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 120;

    /// <summary>上限类配置的下界.</summary>
    private const int MinLimit = 1;

    /// <summary>上限类配置的上界.</summary>
    private const int MaxLimit = 50;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal) { "quote", "news", "search" };

    private static readonly HashSet<string> Ranges = new(StringComparer.Ordinal)
    {
        "1d", "5d", "1mo", "3mo", "6mo", "1y", "2y", "5y", "10y", "ytd", "max",
    };

    private static readonly HashSet<string> Intervals = new(StringComparer.Ordinal)
    {
        "1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h", "1d", "5d", "1wk", "1mo", "3mo",
    };

    private readonly IYahooFinanceClient _client;
    private YahooFinanceQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="YahooFinancePlugin"/> class.
    /// </summary>
    /// <param name="client">雅虎财经客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public YahooFinancePlugin(IYahooFinanceClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "quote",          // quote | news | search
              "Symbols": "AAPL,MSFT",   // quote 必填：股票代码，逗号分隔
              "Query": "",              // news/search 必填：关键词或代码
              "NewsCount": 10,          // news：返回条数
              "SearchCount": 5,         // search：返回候选数
              "Range": "1d",            // quote IncludeHistory=true 时的区间
              "Interval": "1d",         // quote IncludeHistory=true 时的粒度
              "IncludeHistory": false,  // quote：是否附带近期收盘序列（最近 30 点）
              "Lang": "",               // 覆盖实例默认语言，如 zh-CN
              "Region": ""              // 覆盖实例默认市场区域，如 US
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "",       // 数据源地址，留空用官方 https://query1.finance.yahoo.com；可指向镜像/代理/兼容桩服务
              "TimeoutSeconds": 20, // 单次请求超时秒数，1-120
              "MaxSymbols": 10,     // quote 模式单次最多代码数，1-50
              "MaxNews": 10,        // news 模式最多返回条数，1-50
              "MaxSearchResults": 10, // search 模式最多返回候选数，1-50
              "Lang": "zh-CN",      // 默认请求语言，留空不传
              "Region": "US"        // 默认市场区域，留空不传
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(YahooFinanceQueryConfig config)
    {
        var baseUrl = config.BaseUrl?.Trim() ?? string.Empty;
        if (baseUrl.Length > 0)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Task.FromResult<string?>("数据源地址 BaseUrl 必须是合法的 http:// 或 https:// URL，留空使用官方地址");
            }
        }

        _config = new YahooFinanceQueryConfig
        {
            BaseUrl = baseUrl.Length == 0 ? OfficialBaseUrl : EnsureTrailingSlash(baseUrl),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxSymbols = Math.Clamp(config.MaxSymbols, MinLimit, MaxLimit),
            MaxNews = Math.Clamp(config.MaxNews, MinLimit, MaxLimit),
            MaxSearchResults = Math.Clamp(config.MaxSearchResults, MinLimit, MaxLimit),
            Lang = config.Lang?.Trim() ?? string.Empty,
            Region = config.Region?.Trim() ?? string.Empty,
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<YahooFinanceQueryResponse> RunAsync(YahooFinanceQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);

        return mode switch
        {
            "news" => await RunNewsAsync(request, cancellationToken).ConfigureAwait(false),
            "search" => await RunSearchAsync(request, cancellationToken).ConfigureAwait(false),
            _ => await RunQuoteAsync(request, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "quote";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 quote/news/search");
        }

        return normalized;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 每次执行前按实例配置设置 BaseAddress、超时与浏览器 User-Agent（上游对非浏览器 UA 常直接拒绝）；
    /// 客户端为 transient，运行之间互不干扰；实例生命周期内只设一次（HttpClient 首请求后属性不可再改）.
    /// </summary>
    private void PrepareClient()
    {
        if (_client.Client.BaseAddress != null)
        {
            return;
        }

        _client.Client.BaseAddress = new Uri(_config.BaseUrl);
        _client.Client.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        _client.Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        _client.Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    private async Task<YahooFinanceQueryResponse> RunQuoteAsync(YahooFinanceQueryRequest request, CancellationToken cancellationToken)
    {
        var symbols = SplitSymbols(request.Symbols, _config.MaxSymbols, out var truncated);
        if (symbols.Count == 0)
        {
            throw new BusinessException(400, "quote 模式需要提供股票代码 Symbols，多个用英文逗号分隔");
        }

        string? range = null;
        string? interval = null;
        if (request.IncludeHistory)
        {
            range = NormalizeWhitelist(request.Range, Ranges, "1d", "Range");
            interval = NormalizeWhitelist(request.Interval, Intervals, "1d", "Interval");
        }

        PrepareClient();
        var quotes = new List<YahooFinanceQuote>();
        var errors = new List<YahooFinanceErrorItem>();
        foreach (var symbol in symbols)
        {
            try
            {
                var raw = await _client.ChartAsync(symbol, range, interval, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
                quotes.Add(ParseChart(symbol, raw, request.IncludeHistory));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 单代码失败不拖垮整批：无效代码（chart.error）、HTTP 非 2xx 等都归入错误清单继续跑.
                cancellationToken.ThrowIfCancellationRequested();
                errors.Add(new YahooFinanceErrorItem
                {
                    Symbol = symbol,
                    Message = ex is BusinessException businessException ? businessException.Message : DescribeFailure(ex),
                });
            }
        }

        return new YahooFinanceQueryResponse { Mode = "quote", Quotes = quotes, Errors = errors, Truncated = truncated };
    }

    private async Task<YahooFinanceQueryResponse> RunNewsAsync(YahooFinanceQueryRequest request, CancellationToken cancellationToken)
    {
        var query = RequireQuery(request.Query, "news");
        var newsCount = Math.Clamp(request.NewsCount ?? _config.MaxNews, MinLimit, _config.MaxNews);
        PrepareClient();
        var raw = await SendSearchAsync(query, quotesCount: 0, newsCount: newsCount, request, cancellationToken).ConfigureAwait(false);

        using var document = ParseDocument(raw);
        var news = new List<YahooFinanceNewsItem>();
        if (TryGetArray(document.RootElement, "news", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var title = ReadString(item, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                news.Add(new YahooFinanceNewsItem
                {
                    Title = title,
                    Publisher = ReadString(item, "publisher"),
                    Link = ReadString(item, "link"),
                    PublishTime = ReadUnixTimeString(item, "providerPublishTime"),
                    RelatedTickers = ReadStringList(item, "relatedTickers"),
                });
            }
        }

        return new YahooFinanceQueryResponse { Mode = "news", News = news };
    }

    private async Task<YahooFinanceQueryResponse> RunSearchAsync(YahooFinanceQueryRequest request, CancellationToken cancellationToken)
    {
        var query = RequireQuery(request.Query, "search");
        var searchCount = Math.Clamp(request.SearchCount ?? _config.MaxSearchResults, MinLimit, _config.MaxSearchResults);
        PrepareClient();
        var raw = await SendSearchAsync(query, quotesCount: searchCount, newsCount: 0, request, cancellationToken).ConfigureAwait(false);

        using var document = ParseDocument(raw);
        var candidates = new List<YahooFinanceSymbolItem>();
        if (TryGetArray(document.RootElement, "quotes", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var symbol = ReadString(item, "symbol");
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    continue;
                }

                candidates.Add(new YahooFinanceSymbolItem
                {
                    Symbol = symbol,
                    Name = ReadString(item, "shortname") ?? ReadString(item, "longname"),
                    Exchange = ReadString(item, "exchDisp") ?? ReadString(item, "exchange"),
                    TypeDisplay = ReadString(item, "typeDisp"),
                    QuoteType = ReadString(item, "quoteType"),
                });
            }
        }

        return new YahooFinanceQueryResponse { Mode = "search", Candidates = candidates };
    }

    private async Task<string> SendSearchAsync(string query, int quotesCount, int newsCount, YahooFinanceQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SearchAsync(query, quotesCount, newsCount, ResolveLang(request), ResolveRegion(request), cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not BusinessException && ex is not OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(DescribeFailureCode(ex), DescribeFailure(ex));
        }
    }

    private string? ResolveLang(YahooFinanceQueryRequest request)
    {
        return FirstNonEmpty(request.Lang, _config.Lang);
    }

    private string? ResolveRegion(YahooFinanceQueryRequest request)
    {
        return FirstNonEmpty(request.Region, _config.Region);
    }

    private static string? FirstNonEmpty(string? first, string second)
    {
        var value = first?.Trim();
        return string.IsNullOrEmpty(value) ? (string.IsNullOrEmpty(second) ? null : second) : value;
    }

    private static string RequireQuery(string? query, string mode)
    {
        var value = query?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new BusinessException(400, $"{mode} 模式需要提供关键词或股票代码 Query");
        }

        return value;
    }

    private static List<string> SplitSymbols(string? symbols, int max, out bool truncated)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        truncated = false;
        if (string.IsNullOrWhiteSpace(symbols))
        {
            return result;
        }

        foreach (var part in symbols.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!seen.Add(part))
            {
                continue;
            }

            if (result.Count >= max)
            {
                truncated = true;
                break;
            }

            result.Add(part);
        }

        return result;
    }

    private static string NormalizeWhitelist(string? value, HashSet<string> whitelist, string fallback, string name)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return fallback;
        }

        if (!whitelist.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 {name}={normalized}");
        }

        return normalized;
    }

    private static int DescribeFailureCode(Exception ex)
    {
        return ex is ApiException apiException ? (int)apiException.StatusCode : 502;
    }

    private static string DescribeFailure(Exception ex)
    {
        if (ex is ApiException apiException)
        {
            // Refit 对非 2xx 直接抛 ApiException，归一为带状态码与响应体的可读信息（401/403/429/5xx 等）.
            return $"雅虎财经调用失败（HTTP {(int)apiException.StatusCode}）：{apiException.Content ?? apiException.ReasonPhrase}";
        }

        return $"雅虎财经调用失败：{ex.Message}";
    }

    private YahooFinanceQuote ParseChart(string symbol, string raw, bool includeHistory)
    {
        using var document = ParseDocument(raw);
        var root = document.RootElement;

        // 上游对无效代码返回 HTTP 200 + chart.error（如代码已退市）.
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("chart", out var chart)
            && chart.ValueKind == JsonValueKind.Object
            && chart.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object)
        {
            var description = ReadString(error, "description") ?? ReadString(error, "code") ?? "未知错误";
            throw new BusinessException(502, $"雅虎财经查询 {symbol} 失败：{description}");
        }

        var result = GetProperty(root, "chart");
        if (!TryGetArray(result, "result", out var results) || results.GetArrayLength() == 0)
        {
            throw new BusinessException(502, $"雅虎财经查询 {symbol} 失败：响应缺少行情数据");
        }

        var first = results[0];
        var meta = GetProperty(first, "meta");
        if (meta.ValueKind != JsonValueKind.Object)
        {
            throw new BusinessException(502, $"雅虎财经查询 {symbol} 失败：响应缺少 meta 行情摘要");
        }

        var price = ReadDouble(meta, "regularMarketPrice");
        var previousClose = ReadDouble(meta, "chartPreviousClose") ?? ReadDouble(meta, "previousClose");
        double? changeAmount = null;
        double? changePercent = null;
        if (price.HasValue && previousClose.HasValue && previousClose.Value != 0)
        {
            changeAmount = Math.Round(price.Value - previousClose.Value, 4);
            changePercent = Math.Round((price.Value - previousClose.Value) * 100 / previousClose.Value, 2);
        }

        return new YahooFinanceQuote
        {
            Symbol = ReadString(meta, "symbol") ?? symbol,
            Name = ReadString(meta, "longName") ?? ReadString(meta, "shortName"),
            Currency = ReadString(meta, "currency"),
            Exchange = ReadString(meta, "fullExchangeName") ?? ReadString(meta, "exchangeName"),
            InstrumentType = ReadString(meta, "instrumentType"),
            Price = price,
            PreviousClose = previousClose,
            ChangeAmount = changeAmount,
            ChangePercent = changePercent,
            DayHigh = ReadDouble(meta, "regularMarketDayHigh"),
            DayLow = ReadDouble(meta, "regularMarketDayLow"),
            Volume = ReadInt64(meta, "regularMarketVolume"),
            FiftyTwoWeekHigh = ReadDouble(meta, "fiftyTwoWeekHigh"),
            FiftyTwoWeekLow = ReadDouble(meta, "fiftyTwoWeekLow"),
            MarketTime = ReadUnixTimeString(meta, "regularMarketTime"),
            History = includeHistory ? ParseHistory(first) : [],
        };
    }

    private static List<YahooFinanceHistoryPoint> ParseHistory(JsonElement first)
    {
        var points = new List<YahooFinanceHistoryPoint>();
        if (!TryGetArray(first, "timestamp", out var timestamps))
        {
            return points;
        }

        var indicators = GetProperty(first, "indicators");
        if (!TryGetArray(indicators, "quote", out var quoteList) || quoteList.GetArrayLength() == 0)
        {
            return points;
        }

        if (!TryGetArray(quoteList[0], "close", out var closes))
        {
            return points;
        }

        var closeArray = closes;
        var timestampArray = timestamps;
        var limit = Math.Min(timestampArray.GetArrayLength(), closeArray.GetArrayLength());
        for (var i = 0; i < limit; i++)
        {
            if (closeArray[i].ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            points.Add(new YahooFinanceHistoryPoint
            {
                Time = timestampArray[i].ValueKind == JsonValueKind.Number && timestampArray[i].TryGetInt64(out var seconds)
                    ? ToIsoString(seconds)
                    : null,
                Close = closeArray[i].GetDouble(),
            });
        }

        if (points.Count > MaxHistoryPoints)
        {
            points = points.GetRange(points.Count - MaxHistoryPoints, MaxHistoryPoints);
        }

        return points;
    }

    private static string ToIsoString(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string? ReadUnixTimeString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var seconds)
            ? ToIsoString(seconds)
            : null;
    }

    private static JsonDocument ParseDocument(string raw)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "雅虎财经响应不是合法 JSON，请检查数据源地址是否正确");
        }
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    private static bool TryGetArray(JsonElement parent, string name, out JsonElement value)
    {
        value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Array;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.ToString(),
            _ => null,
        };
    }

    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long? ReadInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static List<string> ReadStringList(JsonElement element, string propertyName)
    {
        var result = new List<string>();
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } value)
            {
                result.Add(value);
            }
        }

        return result;
    }
}
