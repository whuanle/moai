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
