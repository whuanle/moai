using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using MoAI.Subsystem.Client.ApiSdk;
using MoAI.Subsystem.Client.ApiSdk.Models;
using Xunit;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 协作面客户端核心行为：token 换取/主动刷新、appid 直连、401 重试、错误语义、DI 装配与请求形状。
/// </summary>
public class SubsysClientTests
{
    private const string TokenJson = """
        {"accessToken":"acc-1","refreshToken":"ref-1","expiresIn":7200,"tokenType":"Bearer"}
        """;

    private const string RefreshedTokenJson = """
        {"accessToken":"acc-2","refreshToken":"ref-2","expiresIn":7200,"tokenType":"Bearer"}
        """;

    [Fact]
    public async Task MachineToken_ShouldFetchOnceAndAttachBearer()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", _ => StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson))
            .On("GET /api/subsys/info", _ =>
            {
                var info = """{"id":"00000000-0000-0000-0000-000000000001","key":"k","name":"n"}""";
                return StubSubsysHandler.Json(HttpStatusCode.OK, info);
            });

        var client = CreateClient(stub);
        var info = await client.Api.Subsys.Info.GetAsync();

        Assert.NotNull(info);
        Assert.Equal(1, stub.Count("POST /api/subsys/token"));
        Assert.Equal("Bearer acc-1", stub.Capture("GET", "/api/subsys/info").Authorization);
        Assert.Equal("n", info!.Name);
    }

    [Fact]
    public async Task MachineToken_ShouldRefreshWhenNearExpiry()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", _ => StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson))
            .On("POST /api/subsys/token/refresh", req =>
            {
                var refreshToken = JsonDocument.Parse(req.Body!).RootElement.GetProperty("refreshToken").GetString();
                Assert.Equal("ref-1", refreshToken);
                return StubSubsysHandler.Json(HttpStatusCode.OK, RefreshedTokenJson);
            })
            .OnByInvocation("GET /api/subsys/info", (req, invocation) =>
            {
                Assert.Equal($"Bearer acc-{invocation + 1}", req.Authorization);
                return StubSubsysHandler.Json(HttpStatusCode.OK, "{}");
            });

        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var client = CreateClient(stub, refreshSkewSeconds: 600, timeProvider: time);

        await client.Api.Subsys.Info.GetAsync();

        // 推进 115 分钟：距过期仅剩 300s < skew 600s → 下一次请求应先 refresh 再调用
        time.Now = time.Now.AddMinutes(115);
        await client.Api.Subsys.Info.GetAsync();

        Assert.Equal(1, stub.Count("POST /api/subsys/token"));
        Assert.Equal(1, stub.Count("POST /api/subsys/token/refresh"));
        Assert.Equal(2, stub.Count("GET", "/api/subsys/info"));
    }

    [Fact]
    public async Task AppIdDirect_ShouldUseAppIdAsBearerWithoutTokenFetch()
    {
        var stub = new StubSubsysHandler()
            .On("GET /api/subsys/info", _ => StubSubsysHandler.Json(HttpStatusCode.OK, "{}"));

        var client = CreateClient(stub, appId: "moai-sa-0123456789abcdef0123456789abcdef01", secret: string.Empty);
        await client.Api.Subsys.Info.GetAsync();

        Assert.Equal(0, stub.Count("POST /api/subsys/token"));
        Assert.Equal("Bearer moai-sa-0123456789abcdef0123456789abcdef01", stub.Capture("GET", "/api/subsys/info").Authorization);
    }

    [Fact]
    public async Task Unauthorized_ShouldInvalidateAndRetryOnce()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", _ => StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson))
            .On("POST /api/subsys/token/refresh", _ => StubSubsysHandler.Json(HttpStatusCode.OK, RefreshedTokenJson))
            .OnByInvocation("GET /api/subsys/info", (req, invocation) =>
            {
                Assert.Equal($"Bearer acc-{invocation + 1}", req.Authorization);
                if (invocation == 0)
                {
                    var tokenExpired = """{"error":{"type":"authentication_error","code":"invalid_token","message":"token expired"}}""";
                    return StubSubsysHandler.Json(HttpStatusCode.Unauthorized, tokenExpired);
                }

                return StubSubsysHandler.Json(HttpStatusCode.OK, "{}");
            });

        var client = CreateClient(stub);
        await client.Api.Subsys.Info.GetAsync();

        Assert.Equal(2, stub.Count("GET", "/api/subsys/info"));
        Assert.Equal(1, stub.Count("POST /api/subsys/token/refresh"));
        Assert.Equal(1, stub.Count("POST /api/subsys/token"));
    }

    [Fact]
    public async Task PermissionError_ShouldThrowSubsysExceptionWithServerSemantics()
    {
        var appDisabled = """{"error":{"type":"permission_error","code":"subsys_app_disabled","message":"接入已停用"}}""";
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", _ => StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson))
            .On("POST /api/subsys/token/refresh", _ => StubSubsysHandler.Json(HttpStatusCode.Unauthorized, "{}"))
            .On("GET /api/subsys/info", _ => StubSubsysHandler.Json(HttpStatusCode.Forbidden, appDisabled));

        var client = CreateClient(stub);
        var exception = await Assert.ThrowsAsync<SubsysException>(() => client.Api.Subsys.Info.GetAsync());

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal("permission_error", exception.ErrorType);
        Assert.Equal("subsys_app_disabled", exception.ErrorCode);
        Assert.Contains("接入已停用", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Heartbeat_ShouldSerializeCommandShape()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", _ => StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson))
            .On("POST /api/subsys/heartbeat", _ => StubSubsysHandler.Json(HttpStatusCode.OK, "{}"));

        var client = CreateClient(stub);
        var subsystemId = Guid.CreateVersion7();
        await client.Api.Subsys.Heartbeat.PostAsync(new SubsystemHeartbeatCommand
        {
            SubsystemId = subsystemId,
            InstanceId = "i-1",
        });

        var captured = stub.Capture("POST", "/api/subsys/heartbeat");
        var body = JsonDocument.Parse(captured.Body!).RootElement;
        Assert.Equal(subsystemId, Guid.Parse(body.GetProperty("subsystemId").GetString()!));
        Assert.Equal("i-1", body.GetProperty("instanceId").GetString());
        Assert.Equal("Bearer acc-1", captured.Authorization);
    }

    [Fact]
    public async Task DependencyInjection_ShouldResolveAndCallThroughFactoryPipeline()
    {
        var stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", _ => StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson))
            .On("GET /api/subsys/info", _ => StubSubsysHandler.Json(HttpStatusCode.OK, """{"online":true}"""));

        var services = new ServiceCollection();
        services.AddMoaiSubsysClient(o =>
        {
            o.BaseUrl = "http://stub.local";
            o.AppId = "my-subsystem";
            o.Secret = "secret-1";
        });
        services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(o =>
        {
            o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = stub);
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<SubsysClient>();
        var info = await client.Api.Subsys.Info.GetAsync();

        Assert.True(info!.Online);
        Assert.NotNull(provider.GetRequiredService<ISubsysTokenProvider>());
    }

    [Fact]
    public async Task RefreshFailure_ShouldFallBackToSecretAndSucceed()
    {
        StubSubsysHandler stub = null!;
        stub = new StubSubsysHandler()
            .On("POST /api/subsys/token", req =>
            {
                var body = JsonDocument.Parse(req.Body!).RootElement;
                if (stub.Count("POST /api/subsys/token") == 1)
                {
                    Assert.Equal(("my-subsystem", "secret-1"), (body.GetProperty("appId").GetString(), body.GetProperty("secret").GetString()));
                    return StubSubsysHandler.Json(HttpStatusCode.OK, TokenJson);
                }

                return StubSubsysHandler.Json(HttpStatusCode.OK, RefreshedTokenJson);
            })
            .On("POST /api/subsys/token/refresh", _ =>
            {
                var refreshRevoked = """{"error":{"type":"authentication_error","code":"invalid_token","message":"refresh revoked"}}""";
                return StubSubsysHandler.Json(HttpStatusCode.Unauthorized, refreshRevoked);
            })
            .OnByInvocation("GET /api/subsys/info", (req, invocation) =>
            {
                Assert.Equal($"Bearer acc-{invocation + 1}", req.Authorization);
                return StubSubsysHandler.Json(HttpStatusCode.OK, "{}");
            });

        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var client = CreateClient(stub, secret: "secret-1", refreshSkewSeconds: 600, timeProvider: time);

        await client.Api.Subsys.Info.GetAsync();
        time.Now = time.Now.AddMinutes(115);
        await client.Api.Subsys.Info.GetAsync();

        // refreshToken 已吊销 → 全量换取回退成功
        Assert.Equal(2, stub.Count("POST /api/subsys/token"));
        Assert.Equal(1, stub.Count("POST /api/subsys/token/refresh"));
        Assert.Equal("Bearer acc-2", stub.Requests.Last(r => r.Uri.AbsolutePath == "/api/subsys/info").Authorization);
    }

    /// <summary>手写装配（不走 DI）：单个 token 管理器同时供重试处理器与认证提供器使用，保证 Invalidate 联动。</summary>
    private static SubsysClient CreateClient(
        StubSubsysHandler stub,
        string appId = "my-subsystem",
        string secret = "secret-1",
        int refreshSkewSeconds = 300,
        TimeProvider? timeProvider = null)
    {
        var options = new MoaiSubsysClientOptions
        {
            BaseUrl = "http://stub.local",
            AppId = appId,
            Secret = secret,
            RefreshSkewSeconds = refreshSkewSeconds,
        };

        ISubsysTokenProvider tokenProvider = CreateTokenProvider(stub, options, timeProvider);
        var retryHandler = new SubsysAuthRetryHandler(tokenProvider) { InnerHandler = stub };
        var httpClient = new HttpClient(retryHandler)
        {
            BaseAddress = new Uri(options.BaseUrl),
        };
        var adapter = new HttpClientRequestAdapter(
            new BaseBearerTokenAuthenticationProvider(tokenProvider),
            httpClient: httpClient);
        adapter.BaseUrl = options.BaseUrl;
        return new SubsysClient(adapter);
    }

    private static SubsysTokenProvider CreateTokenProvider(StubSubsysHandler stub, MoaiSubsysClientOptions options, TimeProvider? timeProvider)
    {
        var tokenHttpClient = new HttpClient(new SubsysErrorHandler() { InnerHandler = stub })
        {
            BaseAddress = new Uri(options.BaseUrl),
        };
        var tokenAdapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(),
            httpClient: tokenHttpClient);
        tokenAdapter.BaseUrl = options.BaseUrl;
        return new SubsysTokenProvider(new SubsysClient(tokenAdapter), options, timeProvider);
    }
}
