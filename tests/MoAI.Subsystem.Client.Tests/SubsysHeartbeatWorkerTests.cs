using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoAI.Subsystem.Client;
using MoAI.Subsystem.Client.Resources;
using Xunit;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 心跳后台宿主：启动即上报实例在场，停止时退出循环。
/// </summary>
public class SubsysHeartbeatWorkerTests
{
    [Fact]
    public async Task 心跳宿主_启动即上报并保持实例标识稳定()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/heartbeat", _ => StubSubsysHandler.Json(HttpStatusCode.OK,
                """{"serverTime":"2026-11-15T08:00:00Z","onlineInstances":1,"windowSeconds":60,"recommendedIntervalSeconds":20}"""));
        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://moai.example.com/") };
        var options = new MoaiSubsysClientOptions
        {
            BaseUrl = "https://moai.example.com",
            AppId = "subsystem-a",
            Secret = "secret",
            Heartbeat = { IntervalSeconds = 60, Version = "1.0.0" },
        };
        var resourceClient = new SubsysResourceClient(
            new SingleClientFactory(httpClient),
            new StubTokenProvider(),
            Options.Create(options));
        var worker = new SubsysHeartbeatWorker(resourceClient, Options.Create(options), NullLogger<SubsysHeartbeatWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => stub.Count("POST", "/api/subsys/heartbeat") == 1);
        await worker.StopAsync(CancellationToken.None);

        var captured = stub.Capture("POST", "/api/subsys/heartbeat");
        var body = System.Text.Json.JsonDocument.Parse(captured.Body!).RootElement;
        Assert.False(string.IsNullOrWhiteSpace(worker.InstanceId));
        Assert.Equal(worker.InstanceId, body.GetProperty("instanceId").GetString());
        Assert.Equal("1.0.0", body.GetProperty("version").GetString());
        Assert.Equal("Bearer token-1", captured.Authorization);
    }

    [Fact]
    public void 未配置实例标识时自动生成且长度合规()
    {
        var options = new SubsysHeartbeatOptions();

        var instanceId = options.ResolveInstanceId();

        Assert.InRange(instanceId.Length, 1, 64);
        Assert.Equal(instanceId, options.ResolveInstanceId());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

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
