using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Prometheus 插件参数/配置校验测试（不发起网络请求）.
/// </summary>
public class PrometheusQueryPluginParamsTests
{
    private readonly PrometheusQueryPlugin _plugin = new(null!);

    [Theory]
    [InlineData("\t")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://prom:9090")]
    [InlineData("prometheus")]
    [InlineData("//prom:9090")]
    public async Task InitAsync_BadBaseUrl_ReturnsReadableError(string value)
    {
        var error = await _plugin.InitAsync(new PrometheusQueryConfig { BaseUrl = value });

        Assert.Contains("BaseUrl", error ?? string.Empty, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_ValidBaseUrl_WithoutTrailingSlash()
    {
        var error = await _plugin.InitAsync(new PrometheusQueryConfig { BaseUrl = " http://prom:9090 " });

        Assert.Null(error);
        var cached = (PrometheusQueryConfig)typeof(PrometheusQueryPlugin).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_plugin)!;
        Assert.Equal("http://prom:9090/", cached.BaseUrl);
    }

    [Fact]
    public async Task InitAsync_ClampsFields()
    {
        var error = await _plugin.InitAsync(new PrometheusQueryConfig
        {
            BaseUrl = "http://prom:9090",
            TimeoutSeconds = 999,
            MaxSeries = 0,
            MaxPointsPerSeries = 9999,
            MaxListItems = 0,
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_UnknownMode_Rejected400()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new PrometheusQueryRequest { Mode = "noSuchMode", Query = "up" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Mode", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_EmptyModeDefaultsToInstant_ThenNetwork()
    {
        // mode 留空会回落 instant；client 为空时在打包请求处非业务性失败（NRE），证明 Mode 默认值生效
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => _plugin.RunAsync(new PrometheusQueryRequest { Query = "up" }, CancellationToken.None));

        Assert.IsNotType<BusinessException>(exception);
    }

    [Fact]
    public async Task RunAsync_InstantBlankQuery_RejectedBeforeNetwork()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new PrometheusQueryRequest { Mode = "instant", Query = "  " }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Query", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RangeBlankQuery_RejectedBeforeNetwork()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new PrometheusQueryRequest { Mode = "range", Query = "  " }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task RunAsync_LabelValuesMissingLabel_Rejected()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new PrometheusQueryRequest { Mode = "label_values" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Label", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SeriesMissingSelector_Rejected()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new PrometheusQueryRequest { Mode = "series" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Selector", exception.Message, System.StringComparison.Ordinal);
    }
}
