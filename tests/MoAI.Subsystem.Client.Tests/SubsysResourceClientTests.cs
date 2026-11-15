using System.Net;
using Microsoft.Extensions.Options;
using MoAI.Subsystem.Client;
using MoAI.Subsystem.Client.Events;
using MoAI.Subsystem.Client.Resources;
using Xunit;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 补充资源客户端（授权快照/事件补发/团队成员）与事件游标测试。
/// </summary>
public class SubsysResourceClientTests
{
    [Fact]
    public async Task GetGrantsAsync_请求授权快照并反序列化()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/grants", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"teamScope":"specifiedTeams","teamIds":[12,18],"orgGrants":[{"orgRoleId":3,"scope":"up"}]}"""));

        var client = CreateClient(stub);
        var grants = await client.GetGrantsAsync();

        Assert.Equal("specifiedTeams", grants.TeamScope);
        Assert.Equal(2, grants.TeamIds.Count);
        Assert.Equal(12L, grants.TeamIds[0]);
        Assert.Equal(18L, grants.TeamIds[1]);
        Assert.Equal(3, grants.OrgGrants[0].OrgRoleId);
        Assert.Equal("up", grants.OrgGrants[0].Scope);
        Assert.Equal("Bearer token-1", stub.Capture("GET", "/api/subsys/grants").Authorization);
    }

    [Fact]
    public async Task GetEventsAsync_携带游标与条数()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/events", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"items":[{"id":"1-0","event":"subsys.user.updated","time":"2026-11-15T00:00:00Z","data":{"userId":"9"}}],"hasMore":true,"resyncRequired":false,"oldestId":"1-0"}"""));

        var client = CreateClient(stub);
        var page = await client.GetEventsAsync("1-0", 50);

        Assert.True(page.HasMore);
        Assert.Single(page.Items);
        Assert.Equal("subsys.user.updated", page.Items[0].Event);
        var captured = stub.Capture("GET", "/api/subsys/events");
        Assert.Contains("afterId=1-0", captured.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("limit=50", captured.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTeamUsersAsync_请求成员清单()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/system/teams/7/users", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"items":[{"userId":1001,"userName":"alice","role":0,"joinTime":"2026-11-15T00:00:00Z"}]}"""));

        var client = CreateClient(stub);
        var members = await client.GetTeamUsersAsync(7);

        Assert.Single(members.Items);
        Assert.Equal(1001, members.Items[0].UserId);
        Assert.Equal("alice", members.Items[0].UserName);
    }

    [Fact]
    public async Task GetInfoAsync_回显事件通道自描述()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/info", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"id":"019a1f0c-7f3e-7c2a-9d61-2b0f6f1c9a11","key":"k","name":"n","appType":"subsystem","status":"enabled","online":true,"onlineInstances":2,"events":{"hubPath":"/hub/subsys","restPath":"/api/subsys/events","catalogPath":"/api/subsys/events/catalog","schemaVersion":1,"retentionDays":2,"maxReplay":200}}"""));

        var client = CreateClient(stub);
        var info = await client.GetInfoAsync();

        Assert.Equal("subsystem", info.AppType);
        Assert.Equal(2, info.OnlineInstances);
        Assert.Equal(1, info.Events.SchemaVersion);
        Assert.Equal(2, info.Events.RetentionDays);
        Assert.Equal("/api/subsys/events/catalog", info.Events.CatalogPath);
    }

    [Fact]
    public async Task QueryEventsAsync_携带事件过滤与游标()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/events", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"items":[],"hasMore":true,"resyncRequired":false,"oldestId":"1-0","nextCursor":"1:9","schemaVersion":1,"retentionDays":2}"""));

        var client = CreateClient(stub);
        var page = await client.QueryEventsAsync("1-3", 50, null, "subsys.team.");

        Assert.True(page.HasMore);
        Assert.Equal("1:9", page.NextCursor);
        Assert.Equal(2, page.RetentionDays);
        var query = stub.Capture("GET", "/api/subsys/events").Uri.Query;
        Assert.Contains("afterId=1-3", query, StringComparison.Ordinal);
        Assert.Contains("limit=50", query, StringComparison.Ordinal);
        Assert.Contains("eventPrefix=subsys.team.", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadEventsAsync_按下一游标翻页()
    {
        var stub = new StubSubsysHandler()
            .OnByInvocation("GET /api/subsys/events", (_, invocation) => StubSubsysHandler.Json(HttpStatusCode.OK, invocation == 0
                ? """{"items":[{"id":"1:4","event":"subsys.user.created","data":{"userId":"1"}}],"hasMore":true,"nextCursor":"1:4"}"""
                : """{"items":[{"id":"1:5","event":"subsys.user.updated","data":{"userId":"1"}}],"hasMore":false}"""));

        var client = CreateClient(stub);
        var ids = new List<string>();
        await foreach (var item in client.ReadEventsAsync(maxPages: 5))
        {
            ids.Add(item.Id);
        }

        Assert.Equal(["1:4", "1:5"], ids);
        Assert.Equal(2, stub.Count("GET", "/api/subsys/events"));
        Assert.Contains("afterId=1%3A4", stub.Requests[1].Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEventsAsync_非2xx转换为SubsysException()
    {
        var appDisabled = """{"error":{"type":"permission_error","code":"subsys_app_disabled","message":"接入已停用"}}""";
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/events", _ => StubSubsysHandler.Json(HttpStatusCode.Forbidden, appDisabled));

        var client = CreateClient(stub);
        var exception = await Assert.ThrowsAsync<SubsysException>(() => client.GetEventsAsync());

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal("subsys_app_disabled", exception.ErrorCode);
    }

    [Fact]
    public async Task HeartbeatAsync_发送实例标识并解析窗口口径()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/heartbeat", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"serverTime":"2026-11-15T08:00:00Z","onlineInstances":3,"windowSeconds":60,"recommendedIntervalSeconds":20}"""));

        var client = CreateClient(stub);
        var result = await client.HeartbeatAsync("instance-a", "1.2.3");

        Assert.Equal(3, result.OnlineInstances);
        Assert.Equal(20, result.RecommendedIntervalSeconds);
        var body = System.Text.Json.JsonDocument.Parse(stub.Capture("POST", "/api/subsys/heartbeat").Body!).RootElement;
        Assert.Equal("instance-a", body.GetProperty("instanceId").GetString());
        Assert.Equal("1.2.3", body.GetProperty("version").GetString());
        Assert.Equal("Bearer token-1", stub.Capture("POST", "/api/subsys/heartbeat").Authorization);
    }

    [Fact]
    public async Task ListUsersAsync_携带分页与搜索参数()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/system/users/list", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"items":[{"id":"1001","userName":"alice","isDisable":false}],"totalCount":1}"""));

        var client = CreateClient(stub);
        var page = await client.ListUsersAsync(2, 50, "ali ce&x");

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("1001", page.Items![0].Id);
        var query = stub.Capture("GET", "/api/subsys/system/users/list").Uri.Query;
        Assert.Contains("pageNo=2", query, StringComparison.Ordinal);
        Assert.Contains("pageSize=50", query, StringComparison.Ordinal);
        Assert.Contains("searchText=ali%20ce%26x", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListTeamsAsync_解析团队列表()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/system/teams/list", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"items":[{"teamId":"7","name":"team-a","memberCount":2}]}"""));

        var client = CreateClient(stub);
        var teams = await client.ListTeamsAsync();

        Assert.Single(teams.Items!);
        Assert.Equal("7", teams.Items![0].TeamId);
        Assert.Equal("team-a", teams.Items![0].Name);
    }

    [Fact]
    public async Task GetEventCatalogAsync_解析事件目录()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/events/catalog", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"schemaVersion":1,"retentionDays":2,"items":[{"event":"subsys.user.created","audience":"all","dataFields":["userId","userName"]},{"event":"subsys.resync_required","audience":"control","dataFields":["reason"]}]}"""));

        var client = CreateClient(stub);
        var catalog = await client.GetEventCatalogAsync();

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(2, catalog.Items.Count);
        Assert.Equal("control", catalog.Items[1].Audience);
        Assert.Contains("userId", catalog.Items[0].DataFields);
    }

    [Fact]
    public async Task CursorStore_异步默认实现委托同步()
    {
        ISubsysEventCursorStore store = new InMemorySubsysEventCursorStore();

        await store.SaveLastEventIdAsync("20261115:1");
        Assert.Equal("20261115:1", await store.GetLastEventIdAsync());
        await store.ClearLastEventIdAsync();
        Assert.Null(await store.GetLastEventIdAsync());
    }

    [Fact]
    public void InMemoryCursorStore_保存并读取()
    {
        ISubsysEventCursorStore store = new InMemorySubsysEventCursorStore();
        Assert.Null(store.GetLastEventId());
        store.SaveLastEventId("1730000000000-0");
        Assert.Equal("1730000000000-0", store.GetLastEventId());
        store.ClearLastEventId();
        Assert.Null(store.GetLastEventId());
    }

    private static SubsysResourceClient CreateClient(StubSubsysHandler stub)
    {
        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://moai.example.com/") };
        var options = Options.Create(new MoaiSubsysClientOptions
        {
            BaseUrl = "https://moai.example.com",
            AppId = "demo",
            Secret = "secret",
        });
        return new SubsysResourceClient(new StubHttpClientFactory(httpClient), new StubTokenProvider(), options);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubTokenProvider : ISubsysTokenProvider
    {
        public Microsoft.Kiota.Abstractions.Authentication.AllowedHostsValidator AllowedHostsValidator => new();

        public void Invalidate()
        {
        }

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
            => Task.FromResult("token-1");
    }
}
