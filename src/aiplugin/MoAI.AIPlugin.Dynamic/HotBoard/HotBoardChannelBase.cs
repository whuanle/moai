using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.HotBoard;

/// <summary>
/// 热榜渠道基类：HTTP 取数（浏览器指纹头 + 可选网关改写）、JSON/文本容错读取与归一化条目构造.
/// 上游多为网页端接口，缺浏览器头会被风控；非法响应统一包装为 502 业务异常.
/// </summary>
public abstract partial class HotBoardChannelBase : IHotBoardChannel
{
    /// <summary>
    /// 浏览器指纹档案（UA 与 Sec-Ch-Ua 等派生字段严格配套；Firefox 无客户端提示，不掺入混配）.
    /// </summary>
    private sealed record BrowserProfile(string UserAgent, string? SecChUa, string SecChUaPlatform, string AcceptLanguage);

    private static readonly BrowserProfile[] BrowserProfiles =
    [
        new(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            "\"Google Chrome\";v=\"123\", \"Not:A-Brand\";v=\"8\", \"Chromium\";v=\"123\"",
            "\"Windows\"",
            "zh-CN,zh;q=0.9,en;q=0.8"),
        new(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            "\"Google Chrome\";v=\"123\", \"Not:A-Brand\";v=\"8\", \"Chromium\";v=\"123\"",
            "\"macOS\"",
            "zh-CN,zh;q=0.9,en;q=0.8"),
        new(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36 Edg/123.0.0.0",
            "\"Microsoft Edge\";v=\"123\", \"Not:A-Brand\";v=\"8\", \"Chromium\";v=\"123\"",
            "\"Windows\"",
            "zh-CN,zh;q=0.9,en;q=0.8"),
        new(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
            null,
            "\"Windows\"",
            "zh-CN,zh;q=0.9,en;q=0.8"),
    ];

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotBoardChannelBase"/> class.
    /// </summary>
    /// <param name="httpClient">源拉取专用 HTTP 客户端.</param>
    /// <param name="gateway">调试网关地址（空串/null 表示直连官方）.</param>
    protected HotBoardChannelBase(HttpClient httpClient, string? gateway)
    {
        _httpClient = httpClient;
        Gateway = string.IsNullOrWhiteSpace(gateway) ? null : gateway.TrimEnd('/');
    }

    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract Task<IReadOnlyList<HotBoardEntry>> FetchAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 调试网关前缀（null 表示直连官方）.
    /// </summary>
    protected string? Gateway { get; }

    /// <summary>
    /// 解析最终请求地址：配置了网关时改写为 {Gateway}/https/{域名}/{路径}，用于镜像、抓包或本地桩服务.
    /// </summary>
    /// <param name="url">官方地址.</param>
    /// <returns>返回实际请求地址.</returns>
    protected string Url(string url)
    {
        if (Gateway is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return $"{Gateway}/https/{uri.Host}{uri.PathAndQuery}";
    }

    /// <summary>
    /// GET 请求并解析 JSON 响应（可按需配置请求头，如 Referer/Cookie）.
    /// </summary>
    /// <param name="url">官方地址.</param>
    /// <param name="configureRequest">请求头配置（可选）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="JsonDocument"/>（调用方负责释放）.</returns>
    protected async Task<JsonDocument> GetJsonAsync(string url, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(url, configureRequest, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ParseJsonAsync(stream, cancellationToken);
    }

    /// <summary>
    /// GET 请求并返回原始响应文本（HTML/JS 变量壳等文本源）.
    /// </summary>
    /// <param name="url">官方地址.</param>
    /// <param name="configureRequest">请求头配置（可选）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回响应正文文本.</returns>
    protected async Task<string> GetStringAsync(string url, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(url, configureRequest, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// POST JSON 请求并解析 JSON 响应.
    /// </summary>
    /// <param name="url">官方地址.</param>
    /// <param name="jsonBody">JSON 请求体.</param>
    /// <param name="configureRequest">请求头配置（可选，Content-Type 默认 application/json）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="JsonDocument"/>（调用方负责释放）.</returns>
    protected async Task<JsonDocument> PostJsonAsync(string url, string jsonBody, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url(url))
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        configureRequest?.Invoke(request);
        ApplyBrowserHeaders(request);
        using var response = await SendCoreAsync(request, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ParseJsonAsync(stream, cancellationToken);
    }

    /// <summary>
    /// GET 请求并读取指定响应头的全部值（如 Set-Cookie），响应体随即释放.
    /// </summary>
    /// <param name="url">官方地址.</param>
    /// <param name="headerName">响应头名称（如 Set-Cookie）.</param>
    /// <param name="configureRequest">请求头配置（可选）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回该头的全部值，缺失返回空列表.</returns>
    protected async Task<IReadOnlyList<string>> GetResponseHeaderValuesAsync(string url, string headerName, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(url));
        configureRequest?.Invoke(request);
        ApplyBrowserHeaders(request);
        using var response = await SendCoreAsync(request, cancellationToken);
        if (!response.Headers.TryGetValues(headerName, out var values))
        {
            return Array.Empty<string>();
        }

        return values.ToList();
    }

    /// <summary>
    /// 读取对象字段字符串，缺失或非字符串返回 null.
    /// </summary>
    /// <param name="element">JSON 对象.</param>
    /// <param name="name">字段名.</param>
    /// <returns>返回字符串或 null.</returns>
    protected static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 读取对象字段标量（字符串/数字字面量，其它类型返回 null）；长整型 id 取原始文本避免精度丢失.
    /// </summary>
    /// <param name="element">JSON 对象.</param>
    /// <param name="name">字段名.</param>
    /// <returns>返回标量文本或 null.</returns>
    protected static string? GetScalar(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(property.GetString()) ? null : property.GetString()!.Trim(),
            JsonValueKind.Number => property.ToString(),
            _ => null,
        };
    }

    /// <summary>
    /// 读取对象嵌套对象的字符串字段（如 Image.url）.
    /// </summary>
    /// <param name="element">JSON 对象.</param>
    /// <param name="name">外层字段名.</param>
    /// <param name="childName">内层字段名.</param>
    /// <returns>返回字符串或 null.</returns>
    protected static string? GetNestedString(JsonElement element, string name, string childName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var nested)
            || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(nested, childName);
    }

    /// <summary>
    /// 读取对象嵌套对象的标量字段（如 stat.view）.
    /// </summary>
    /// <param name="element">JSON 对象.</param>
    /// <param name="name">外层字段名.</param>
    /// <param name="childName">内层字段名.</param>
    /// <returns>返回标量文本或 null.</returns>
    protected static string? GetNestedScalar(JsonElement element, string name, string childName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var nested)
            || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetScalar(nested, childName);
    }

    /// <summary>
    /// HTML 片段转纯文本（去标签 + 实体解码 + 空白折叠）.
    /// </summary>
    /// <param name="html">HTML 文本.</param>
    /// <returns>返回纯文本，空白结果返回 null.</returns>
    protected static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(HtmlTagRegex().Replace(html, " "));
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// 构造归一化条目（标题为空的条目由调用方跳过）.
    /// </summary>
    /// <param name="title">标题.</param>
    /// <param name="desc">摘要（可空）.</param>
    /// <param name="url">原文链接（可空）.</param>
    /// <param name="cover">封面图（可空）.</param>
    /// <param name="hotValue">热度值（可空）.</param>
    /// <returns>返回归一化条目.</returns>
    protected static HotBoardEntry Build(string title, string? desc, string? url, string? cover, long? hotValue)
    {
        return new HotBoardEntry
        {
            Title = title.Trim(),
            Desc = desc,
            Url = url?.Trim() ?? string.Empty,
            Cover = cover,
            HotValue = hotValue,
        };
    }

    private static async Task<JsonDocument> ParseJsonAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new BusinessException(502, $"热榜源响应不是有效 JSON（{ex.Message}）");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string url, Action<HttpRequestMessage>? configureRequest, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(url));
        configureRequest?.Invoke(request);
        ApplyBrowserHeaders(request);
        return await SendCoreAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new BusinessException(502, $"热榜源请求失败：无法连接源服务（{ex.Message}）");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = string.Empty;
            try
            {
                body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                if (body.Length > 300)
                {
                    body = body[..300];
                }
            }
            catch
            {
                // 忽略读取失败，保留状态码信息即可
            }

            response.Dispose();
            throw new BusinessException(502, $"热榜源请求失败：HTTP {(int)response.StatusCode} {response.StatusCode} {body}");
        }

        return response;
    }

    /// <summary>
    /// 自动补全浏览器指纹请求头：按目标主机稳定选档（同一上游恒定同一指纹），
    /// 仅填充调用方未显式设置的头（UA/Accept/Accept-Language/Sec-Ch-Ua 系列/Sec-Fetch 系列）.
    /// </summary>
    /// <param name="request">待发送请求.</param>
    private static void ApplyBrowserHeaders(HttpRequestMessage request)
    {
        var host = request.RequestUri?.Host ?? string.Empty;
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in host)
            {
                hash = (hash ^ ch) * 16777619u;
            }

            var profile = BrowserProfiles[(int)(hash % BrowserProfiles.Length)];
            var headers = request.Headers;
            if (headers.UserAgent.Count == 0)
            {
                headers.UserAgent.ParseAdd(profile.UserAgent);
            }

            TryAddHeader(headers, "Accept", "application/json, text/html, */*;q=0.8");
            TryAddHeader(headers, "Accept-Language", profile.AcceptLanguage);
            if (profile.SecChUa is not null)
            {
                TryAddHeader(headers, "Sec-Ch-Ua", profile.SecChUa);
                TryAddHeader(headers, "Sec-Ch-Ua-Mobile", "?0");
                TryAddHeader(headers, "Sec-Ch-Ua-Platform", profile.SecChUaPlatform);
            }

            TryAddHeader(headers, "Sec-Fetch-Dest", "document");
            TryAddHeader(headers, "Sec-Fetch-Mode", "navigate");
            TryAddHeader(headers, "Sec-Fetch-Site", "none");
            TryAddHeader(headers, "Upgrade-Insecure-Requests", "1");
        }
    }

    private static void TryAddHeader(System.Net.Http.Headers.HttpRequestHeaders headers, string name, string value)
    {
        if (!headers.Contains(name))
        {
            headers.TryAddWithoutValidation(name, value);
        }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
