using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoAI.Subsystem.Client.ApiSdk.Models;
using MoAI.Subsystem.Client.Events;

namespace MoAI.Subsystem.Client.Resources;

/// <summary>
/// 子系统协作面补充资源客户端：授权快照、事件补发/目录、心跳、用户/组织/团队对账。
/// 与 Kiota 生成的 <c>SubsysClient</c> 共用同一套 token 管理与错误转换；新增端点通过本类手写包装暴露，
/// 待 <c>node clients/sync-api.mjs</c> 重新生成后可按需迁移到 <c>Generated/</c>。
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
    public Task<SubsysGrantsResponse> GetGrantsAsync(CancellationToken cancellationToken = default)
        => SendAsync<SubsysGrantsResponse>(HttpMethod.Get, "/api/subsys/grants", cancellationToken: cancellationToken);

    /// <summary>接入自省（含事件通道自描述：Hub 路径/版本/窗口）.</summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>自省结果.</returns>
    public Task<SubsysInfoResponse> GetInfoAsync(CancellationToken cancellationToken = default)
        => SendAsync<SubsysInfoResponse>(HttpMethod.Get, "/api/subsys/info", cancellationToken: cancellationToken);

    /// <summary>查询当前接入可见的最近事件（REST 补发兜底）.</summary>
    /// <param name="afterId">上次事件 id，可空.</param>
    /// <param name="limit">返回上限（1~500）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件页.</returns>
    public Task<SubsysEventPage> GetEventsAsync(string? afterId = null, int limit = 200, CancellationToken cancellationToken = default)
        => QueryEventsAsync(afterId, limit, null, null, cancellationToken);

    /// <summary>按事件名/前缀查询事件页（过滤发生在服务端取 limit 之前）.</summary>
    /// <param name="afterId">上次事件 id，可空.</param>
    /// <param name="limit">返回上限（1~500）.</param>
    /// <param name="eventName">精确事件名，可空.</param>
    /// <param name="eventPrefix">事件名前缀，可空；与 <paramref name="eventName"/> 互斥.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件页（含 <see cref="SubsysEventPage.NextCursor"/>）.</returns>
    public Task<SubsysEventPage> QueryEventsAsync(
        string? afterId,
        int limit,
        string? eventName,
        string? eventPrefix,
        CancellationToken cancellationToken = default)
    {
        var path = BuildPath(
            "/api/subsys/events",
            ("afterId", afterId),
            ("limit", limit.ToString(CultureInfo.InvariantCulture)),
            ("event", eventName),
            ("eventPrefix", eventPrefix));
        return SendAsync<SubsysEventPage>(HttpMethod.Get, path, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 自动翻页读取事件（直到无更多/达到页数上限/游标失效）。
    /// 游标失效（<c>resyncRequired</c>）时直接结束，接入方应改为全量对账。
    /// </summary>
    /// <param name="afterId">起始游标，可空.</param>
    /// <param name="limit">每页条数.</param>
    /// <param name="eventName">精确事件名，可空.</param>
    /// <param name="eventPrefix">事件名前缀，可空.</param>
    /// <param name="maxPages">最大页数，默认 50.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件流.</returns>
    public async IAsyncEnumerable<SubsysEventItem> ReadEventsAsync(
        string? afterId = null,
        int limit = 200,
        string? eventName = null,
        string? eventPrefix = null,
        int maxPages = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var cursor = afterId;
        for (var page = 0; page < Math.Max(1, maxPages); page++)
        {
            var result = await QueryEventsAsync(cursor, limit, eventName, eventPrefix, cancellationToken);
            if (result.ResyncRequired)
            {
                yield break;
            }

            foreach (var item in result.Items)
            {
                yield return item;
            }

            if (!result.HasMore || string.IsNullOrEmpty(result.NextCursor)
                || string.Equals(result.NextCursor, cursor, StringComparison.Ordinal))
            {
                yield break;
            }

            cursor = result.NextCursor;
        }
    }

    /// <summary>查询事件目录（机器可读的事件名/受众/载荷字段）.</summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件目录.</returns>
    public Task<SubsysEventCatalogResponse> GetEventCatalogAsync(CancellationToken cancellationToken = default)
        => SendAsync<SubsysEventCatalogResponse>(HttpMethod.Get, "/api/subsys/events/catalog", cancellationToken: cancellationToken);

    /// <summary>发送心跳并回读在线实例数（建议间隔取响应 <see cref="SubsysHeartbeatResponse.RecommendedIntervalSeconds"/>）.</summary>
    /// <param name="instanceId">实例标识（进程内稳定，1~64 字符）.</param>
    /// <param name="version">上报版本号，可空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>心跳响应.</returns>
    public Task<SubsysHeartbeatResponse> HeartbeatAsync(string instanceId, string? version = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var body = new SubsystemHeartbeatCommand
        {
            InstanceId = instanceId,
            Version = version,
        };
        return SendAsync<SubsysHeartbeatResponse>(HttpMethod.Post, "/api/subsys/heartbeat", body, cancellationToken);
    }

    /// <summary>查询指定用户信息.</summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户状态资料.</returns>
    public Task<UserStateInfo> GetUserAsync(long userId, CancellationToken cancellationToken = default)
        => SendAsync<UserStateInfo>(HttpMethod.Get, $"/api/subsys/system/users/{userId}", cancellationToken: cancellationToken);

    /// <summary>分页查询用户列表.</summary>
    /// <param name="pageNo">页码（1 起）.</param>
    /// <param name="pageSize">每页数量.</param>
    /// <param name="searchText">用户名/昵称/邮箱模糊搜索，可空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户分页.</returns>
    public Task<QueryUserListCommandResponse> ListUsersAsync(
        int pageNo = 1,
        int pageSize = 20,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var path = BuildPath(
            "/api/subsys/system/users/list",
            ("pageNo", pageNo.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            ("searchText", searchText));
        return SendAsync<QueryUserListCommandResponse>(HttpMethod.Get, path, cancellationToken: cancellationToken);
    }

    /// <summary>查询组织角色平铺列表.</summary>
    /// <param name="search">角色名称模糊搜索，可空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>组织角色列表.</returns>
    public Task<QueryOrgRolesCommandResponse> ListOrgRolesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var path = BuildPath("/api/subsys/system/org/roles", ("search", search));
        return SendAsync<QueryOrgRolesCommandResponse>(HttpMethod.Get, path, cancellationToken: cancellationToken);
    }

    /// <summary>查询某用户持有的组织角色.</summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户组织角色.</returns>
    public Task<QueryUserOrgRolesCommandResponse> GetUserOrgRolesAsync(long userId, CancellationToken cancellationToken = default)
        => SendAsync<QueryUserOrgRolesCommandResponse>(HttpMethod.Get, $"/api/subsys/system/org/users/{userId}/roles", cancellationToken: cancellationToken);

    /// <summary>查询某组织角色下的用户清单.</summary>
    /// <param name="roleId">组织角色 id.</param>
    /// <param name="search">用户名/昵称模糊搜索，可空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>角色用户列表.</returns>
    public Task<QueryOrgRoleUsersCommandResponse> GetOrgRoleUsersAsync(int roleId, string? search = null, CancellationToken cancellationToken = default)
    {
        var path = BuildPath($"/api/subsys/system/org/roles/{roleId}/users", ("search", search));
        return SendAsync<QueryOrgRoleUsersCommandResponse>(HttpMethod.Get, path, cancellationToken: cancellationToken);
    }

    /// <summary>查询全部团队（对账用）.</summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>团队列表.</returns>
    public Task<QueryTeamAllCommandResponse> ListTeamsAsync(CancellationToken cancellationToken = default)
        => SendAsync<QueryTeamAllCommandResponse>(HttpMethod.Get, "/api/subsys/system/teams/list", cancellationToken: cancellationToken);

    /// <summary>查询团队成员清单（供对账）.</summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>成员列表.</returns>
    public Task<SubsysTeamUsersResponse> GetTeamUsersAsync(long teamId, CancellationToken cancellationToken = default)
        => SendAsync<SubsysTeamUsersResponse>(HttpMethod.Get, $"/api/subsys/system/teams/{teamId}/users", cancellationToken: cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        var token = await _tokenProvider.GetAuthorizationTokenAsync(
            new Uri($"{_options.BaseUrl.TrimEnd('/')}{path}"),
            cancellationToken: cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        var client = _httpClientFactory.CreateClient(SubsysClientServiceCollectionExtensions.HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw SubsysException.From(response.StatusCode, content);
        }

        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            throw new SubsysException(response.StatusCode, "服务端返回空响应");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new SubsysException(response.StatusCode, "服务端响应无法解析");
    }

    private static string BuildPath(string path, params (string Key, string? Value)[] query)
    {
        var parts = new List<string>(query.Length);
        foreach (var (key, value) in query)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }
}
