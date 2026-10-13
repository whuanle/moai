using System.Reflection;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 新闻热榜插件配置校验测试（不发起网络请求）.
/// </summary>
public class HotBoardPluginParamsTests
{
    private readonly HotBoardPlugin _plugin = new(null!);

    [Fact]
    public async Task InitAsync_ClampsLimit()
    {
        await _plugin.InitAsync(new HotBoardConfig { Limit = 0 });
        Assert.Equal(1, GetConfig().Limit);

        await _plugin.InitAsync(new HotBoardConfig { Limit = 999 });
        Assert.Equal(50, GetConfig().Limit);

        await _plugin.InitAsync(new HotBoardConfig { Limit = 30 });
        Assert.Equal(30, GetConfig().Limit);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:8080")]
    [InlineData("//127.0.0.1:5280")]
    public async Task InitAsync_BadGateway_ReturnsReadableError(string value)
    {
        var error = await _plugin.InitAsync(new HotBoardConfig { Gateway = value });

        Assert.Contains("Gateway", error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_EmptyGateway_KeepsNull()
    {
        Assert.Null(await _plugin.InitAsync(new HotBoardConfig { Gateway = "   " }));
        Assert.Null(GetConfig().Gateway);
    }

    [Fact]
    public async Task InitAsync_GatewayTrimmedAndClampsLimit()
    {
        Assert.Null(await _plugin.InitAsync(new HotBoardConfig { Gateway = " http://127.0.0.1:5280 ", Limit = 5 }));

        var config = GetConfig();
        Assert.Equal("http://127.0.0.1:5280", config.Gateway);
        Assert.Equal(5, config.Limit);
    }

    [Fact]
    public void GetConfigExampleValue_ContainsLimitAndGateway()
    {
        Assert.Contains("Limit", HotBoardPlugin.GetConfigExampleValue(), StringComparison.Ordinal);
        Assert.Contains("Gateway", HotBoardPlugin.GetConfigExampleValue(), StringComparison.Ordinal);
    }

    [Fact]
    public void GetParamsExampleValue_ContainsSources()
    {
        Assert.Contains("Sources", HotBoardPlugin.GetParamsExampleValue(), StringComparison.Ordinal);
    }

    private HotBoardConfig GetConfig()
    {
        return (HotBoardConfig)typeof(HotBoardPlugin)
            .GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_plugin)!;
    }
}
