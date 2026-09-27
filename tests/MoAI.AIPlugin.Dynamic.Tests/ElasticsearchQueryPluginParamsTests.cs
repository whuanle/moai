using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Elasticsearch 插件参数/配置校验测试（不发起网络请求）.
/// </summary>
public class ElasticsearchQueryPluginParamsTests
{
    private readonly ElasticsearchQueryPlugin _plugin = new(null!);

    [Theory]
    [InlineData("")]
    [InlineData("es no url")]
    [InlineData("ssh://es")]
    public async Task InitAsync_BadBaseUrl_ReturnsReadableError(string value)
    {
        var error = await _plugin.InitAsync(new ElasticsearchQueryConfig { BaseUrl = value });

        Assert.Contains("BaseUrl", error ?? string.Empty, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_ClampsFields()
    {
        var error = await _plugin.InitAsync(new ElasticsearchQueryConfig
        {
            BaseUrl = "http://es:9200",
            TimeoutSeconds = 0,
            MaxHits = 9999,
            MaxSourceCharsPerHit = 1,
            MaxResponseChars = 0,
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_UnknownMode_Rejected400()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ElasticsearchQueryRequest { Mode = "insert" }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Mode", exception.Message, System.StringComparison.Ordinal);
    }
}
