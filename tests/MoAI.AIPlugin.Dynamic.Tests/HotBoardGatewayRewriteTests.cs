using MoAI.AIPlugin.Dynamic.HotBoard.Channels;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 调试网关地址改写测试：配置网关后请求改写为 {Gateway}/https/{域名}/{路径}.
/// </summary>
public class HotBoardGatewayRewriteTests
{
    [Fact]
    public async Task Channel_WithGateway_RewritesRequestUrl()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(new { data = Array.Empty<object>() }));
        using var client = new HttpClient(handler);

        var channel = new ZhihuChannel(client, "http://127.0.0.1:5280/");
        await channel.FetchAsync(CancellationToken.None);

        var uri = Assert.Single(handler.RequestUris);
        Assert.StartsWith("http://127.0.0.1:5280/https/api.zhihu.com/", uri, StringComparison.Ordinal);
        Assert.EndsWith("topstory/hot-lists/total?limit=50", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Channel_WithoutGateway_KeepsOfficialUrl()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(new { data = Array.Empty<object>() }));
        using var client = new HttpClient(handler);

        var channel = new ZhihuChannel(client, null);
        await channel.FetchAsync(CancellationToken.None);

        Assert.StartsWith("https://api.zhihu.com/", Assert.Single(handler.RequestUris), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Channel_InvalidJson_ThrowsBusinessException()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Text("not json"));
        using var client = new HttpClient(handler);

        var channel = new ZhihuChannel(client, null);

        await Assert.ThrowsAsync<BusinessException>(() => channel.FetchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Channel_UpstreamError_ThrowsWithStatusCode()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)
        {
            Content = new StringContent("blocked"),
        });
        using var client = new HttpClient(handler);

        var channel = new WeiboChannel(client, null);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => channel.FetchAsync(CancellationToken.None));
        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("403", ex.Message, StringComparison.Ordinal);
    }
}
