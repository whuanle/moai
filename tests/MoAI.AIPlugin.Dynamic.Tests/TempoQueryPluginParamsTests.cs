using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Tempo 插件参数/配置校验测试（不发起网络请求）.
/// </summary>
public class TempoQueryPluginParamsTests
{
    private readonly TempoQueryPlugin _plugin = new(null!);

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("ftp://tempo:3200")]
    public async Task InitAsync_BadBaseUrl_ReturnsReadableError(string value)
    {
        var error = await _plugin.InitAsync(new TempoQueryConfig { BaseUrl = value });

        Assert.Contains("BaseUrl", error ?? string.Empty, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_ClampsFields()
    {
        var error = await _plugin.InitAsync(new TempoQueryConfig
        {
            BaseUrl = "http://tempo:3200",
            TimeoutSeconds = -1,
            MaxTraces = 0,
            MaxSpans = 99999,
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_UnknownMode_Rejected400()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new TempoQueryRequest { Mode = "graphql" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task RunAsync_TraceqlBlankQuery_RejectedBeforeNetwork()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new TempoQueryRequest { Mode = "traceql", Query = " " }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Query", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_TraceMissingId_RejectedBeforeNetwork()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new TempoQueryRequest { Mode = "trace" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("TraceId", exception.Message, System.StringComparison.Ordinal);
    }
}
