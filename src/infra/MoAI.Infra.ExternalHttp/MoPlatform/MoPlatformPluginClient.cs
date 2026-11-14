using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra;

/// <summary>
/// MoPlatform 插件分发客户端；所有 client/plugin 请求都携带安装指纹.
/// </summary>
public class MoPlatformPluginClient
{
    /// <summary>
    /// HTTP 客户端名称.
    /// </summary>
    public const string HttpClientName = "mo_platform_plugin";

    /// <summary>签名对象存储下载客户端（大文件使用较长传输超时）.</summary>
    public const string DownloadHttpClientName = "mo_platform_plugin_download";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MoPlatformClientFingerprint _fingerprint;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoPlatformPluginClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="fingerprint">安装实例指纹.</param>
    public MoPlatformPluginClient(IHttpClientFactory httpClientFactory, MoPlatformClientFingerprint fingerprint)
    {
        _httpClientFactory = httpClientFactory;
        _fingerprint = fingerprint;
    }

    /// <summary>
    /// 查询已上架插件列表.
    /// </summary>
    /// <param name="keyword">可选关键字.</param>
    /// <param name="pageNo">页码，从 1 开始.</param>
    /// <param name="pageSize">分页大小，平台上限为 100.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>由调用方负责释放的原始 JSON 文档.</returns>
    public Task<JsonDocument> GetPublishedPluginsAsync(
        string? keyword = null,
        string? categoryId = null,
        int pageNo = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = $"?pageNo={pageNo}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query += "&keyword=" + Uri.EscapeDataString(keyword);
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query += "&categoryId=" + Uri.EscapeDataString(categoryId);
        }

        return GetJsonAsync("api/client/plugin/list" + query, cancellationToken);
    }

    /// <summary>查询 MoPlatform 在线插件分类.</summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>由调用方负责释放的原始 JSON 文档.</returns>
    public Task<JsonDocument> GetPluginCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return GetJsonAsync("api/portal/category/list?type=plugin", cancellationToken);
    }

    /// <summary>
    /// 查询已上架插件详情.
    /// </summary>
    /// <param name="pluginId">MoPlatform 插件 id（十进制字符串）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>由调用方负责释放的原始 JSON 文档.</returns>
    public Task<JsonDocument> GetPublishedPluginDetailAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return GetJsonAsync("api/client/plugin/detail?pluginId=" + Uri.EscapeDataString(pluginId), cancellationToken);
    }

    /// <summary>
    /// 查询已上架插件版本的兼容清单.
    /// </summary>
    /// <param name="key">插件 key.</param>
    /// <param name="version">可选技术版本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>由调用方负责释放的原始 JSON 文档.</returns>
    public Task<JsonDocument> GetPluginManifestAsync(string key, string? version = null, CancellationToken cancellationToken = default)
    {
        var query = "?key=" + Uri.EscapeDataString(key);
        if (!string.IsNullOrWhiteSpace(version))
        {
            query += "&version=" + Uri.EscapeDataString(version);
        }

        return GetJsonAsync("api/client/plugin/manifest" + query, cancellationToken);
    }

    /// <summary>
    /// 为明确插件版本申请免用户登录的短期下载地址.
    /// </summary>
    /// <param name="pluginId">MoPlatform 插件 id（十进制字符串）.</param>
    /// <param name="versionId">清单对应版本 id（十进制字符串）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>签名下载地址与文件摘要.</returns>
    public async Task<MoPlatformPluginDownloadResponse> GetDownloadUrlAsync(
        string pluginId,
        string versionId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/client/plugin/download");
        request.Content = JsonContent.Create(new { pluginId, versionId }, options: SerializerOptions);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<MoPlatformPluginDownloadResponse>(SerializerOptions, cancellationToken)
            ?? throw new HttpRequestException("MoPlatform 插件下载响应为空.");
    }

    private async Task<JsonDocument> GetJsonAsync(string uri, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("X-MoAI-Client-Fingerprint", _fingerprint.Fingerprint);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"MoPlatform 插件接口请求失败（HTTP {(int)response.StatusCode}）：{detail}",
                inner: null,
                response.StatusCode);
        }
    }
}

/// <summary>
/// MoPlatform 插件下载地址响应.
/// </summary>
public class MoPlatformPluginDownloadResponse
{
    /// <summary>插件 id.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>插件唯一 key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>版本 id.</summary>
    public string VersionId { get; init; } = string.Empty;

    /// <summary>技术版本.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>包文件名.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>包大小（字节字符串）.</summary>
    public string FileSize { get; init; } = string.Empty;

    /// <summary>包 SHA-256.</summary>
    public string FileSha256 { get; init; } = string.Empty;

    /// <summary>短期预签名下载地址.</summary>
    public Uri DownloadUrl { get; init; } = default!;

    /// <summary>下载地址过期时间.</summary>
    public DateTimeOffset Expiration { get; init; }
}
