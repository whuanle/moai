using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 雅虎财经插件参数/配置校验测试（不发起网络请求）.
/// </summary>
public class YahooFinancePluginParamsTests
{
    private readonly YahooFinancePlugin _plugin = new(null!);

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://finance:8080")]
    [InlineData("yahoo")]
    [InlineData("//query1.finance.yahoo.com")]
    public async Task InitAsync_BadBaseUrl_ReturnsReadableError(string value)
    {
        var error = await _plugin.InitAsync(new YahooFinanceQueryConfig { BaseUrl = value });

        Assert.Contains("BaseUrl", error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_EmptyBaseUrl_FallsBackOfficial()
    {
        var error = await _plugin.InitAsync(new YahooFinanceQueryConfig { BaseUrl = "  " });

        Assert.Null(error);
        var cached = GetConfig();
        Assert.Equal("https://query1.finance.yahoo.com/", cached.BaseUrl);
    }

    [Fact]
    public async Task InitAsync_ValidBaseUrl_AppendsTrailingSlash()
    {
        var error = await _plugin.InitAsync(new YahooFinanceQueryConfig { BaseUrl = " http://127.0.0.1:5210 " });

        Assert.Null(error);
        Assert.Equal("http://127.0.0.1:5210/", GetConfig().BaseUrl);
    }

    [Fact]
    public async Task InitAsync_ClampsFields()
    {
        var error = await _plugin.InitAsync(new YahooFinanceQueryConfig
        {
            BaseUrl = "https://query1.finance.yahoo.com",
            TimeoutSeconds = 999,
            MaxSymbols = 0,
            MaxNews = 999,
            MaxSearchResults = -1,
            Lang = " zh-CN ",
        });

        Assert.Null(error);
        var cached = GetConfig();
        Assert.Equal(120, cached.TimeoutSeconds);
        Assert.Equal(1, cached.MaxSymbols);
        Assert.Equal(50, cached.MaxNews);
        Assert.Equal(1, cached.MaxSearchResults);
        Assert.Equal("zh-CN", cached.Lang);
    }

    [Fact]
    public async Task RunAsync_UnknownMode_Rejected400()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _plugin.RunAsync(new YahooFinanceQueryRequest { Mode = "noSuchMode", Symbols = "AAPL" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Mode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_BlankModeDefaultsToQuote_ThenNetwork()
    {
        // mode 留空会回落 quote；client 为空时在设置 BaseAddress 处非业务性失败（NRE），证明 Mode 默认值生效
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => _plugin.RunAsync(new YahooFinanceQueryRequest { Symbols = "AAPL" }, CancellationToken.None));

        Assert.IsNotType<BusinessException>(exception);
    }

    [Theory]
    [InlineData("", "quote", "Symbols")]
    [InlineData("   ", "quote", "Symbols")]
    [InlineData(",", "quote", "Symbols")]
    [InlineData("  ", "news", "Query")]
    [InlineData("  ", "search", "Query")]
    public async Task RunAsync_MissingRequiredParams_Rejected400(string value, string mode, string expectedField)
    {
        var request = new YahooFinanceQueryRequest { Mode = mode };
        if (mode == "quote")
        {
            request.Symbols = value;
        }
        else
        {
            request.Query = value;
        }

        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(request, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains(expectedField, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Range", "3h")]
    [InlineData("Interval", "7m")]
    public async Task RunAsync_InvalidHistoryParams_Rejected400(string field, string value)
    {
        var request = new YahooFinanceQueryRequest
        {
            Mode = "quote",
            Symbols = "AAPL",
            IncludeHistory = true,
        };
        if (field == "Range")
        {
            request.Range = value;
        }
        else
        {
            request.Interval = value;
        }

        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(request, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains(field, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetConfigExampleValue_ContainsBaseUrl()
    {
        Assert.Contains("BaseUrl", YahooFinancePlugin.GetConfigExampleValue(), StringComparison.Ordinal);
        Assert.Contains("Mode", YahooFinancePlugin.GetParamsExampleValue(), StringComparison.Ordinal);
    }

    private YahooFinanceQueryConfig GetConfig()
    {
        return (YahooFinanceQueryConfig)typeof(YahooFinancePlugin)
            .GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_plugin)!;
    }
}
