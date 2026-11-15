using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoAI.Subsystem.Client.Events;

namespace MoAI.Subsystem.Client.Resources;

/// <summary>
/// 子系统协作面补充资源客户端：授权快照、事件补发、团队成员对账。
/// 与 Kiota 生成的 <c>SubsysClient</c> 共用同一套 token 管理与错误转换。
/// </summary>
public sealed class SubsysResourceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISubsysTokenProvider _tokenProvider;
    private readonly MoaiSubsysClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysResourceClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂.</param>
    /// <param name="tokenProvider">token 提供器.</param>
    /// <param name="options">客户端配置.</param>
    public SubsysResourceClient(
        IHttpClientFactory httpClientFactory,
        ISubsysTokenProvider tokenProvider,
        IOptions<MoaiSubsysClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    /// <summary>查询当前接入自身的授权快照.</summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>授权快照.</returns>
    public async Task<SubsysGrantsResponse> GetGrantsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, "/api/subsys/grants", cancellationToken);
        return await SendAsync<SubsysGrantsResponse>(request, cancellationToken)
            ?? throw new SubsysException(System.Net.HttpStatusCode.BadGateway, "授权快照返回空响应");
    }

    /// <summary>查询当前接入可见的最近事件（REST 补发兜底）.</summary>
    /// <param name="afterId">上次事件 id，可空.</param>
    /// <param name="limit">返回上限.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件页.</returns>
    public async Task<SubsysEventPage> GetEventsAsync(string? afterId = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        var path = $"/api/subsys/events?limit={limit}";
        if (!string.IsNullOrEmpty(afterId))
        {
            path += $"&afterId={Uri.EscapeDataString(afterId)}";
        }

        var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        return await SendAsync<SubsysEventPage>(request, cancellationToken)
            ?? throw new SubsysException(System.Net.HttpStatusCode.BadGateway, "事件补发返回空响应");
    }

    /// <summary>查询团队成员清单（供对账）.</summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>成员列表.</returns>
    public async Task<SubsysTeamUsersResponse> GetTeamUsersAsync(long teamId, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"/api/subsys/system/teams/{teamId}/users", cancellationToken);
        return await SendAsync<SubsysTeamUsersResponse>(request, cancellationToken)
            ?? throw new SubsysException(System.Net.HttpStatusCode.BadGateway, "团队成员返回空响应");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        var token = await _tokenProvider.GetAuthorizationTokenAsync(new Uri($"{_options.BaseUrl.TrimEnd('/')}{path}"), cancellationToken: cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            var client = _httpClientFactory.CreateClient(SubsysClientServiceCollectionExtensions.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
    }
}
