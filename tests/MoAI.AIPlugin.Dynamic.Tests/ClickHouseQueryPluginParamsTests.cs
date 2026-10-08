using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// ClickHouse 插件参数与只读守卫的行为测试（不发起网络请求）.
/// </summary>
public class ClickHouseQueryPluginParamsTests
{
    private readonly ClickHouseQueryPlugin _plugin = new(null!);

    [Fact]
    public async Task InitAsync_BlankBaseUrl_ReturnsError()
    {
        var error = await _plugin.InitAsync(new ClickHouseQueryConfig());

        Assert.Equal("服务地址 BaseUrl 不能为空", error);
    }

    [Fact]
    public async Task InitAsync_FtpBaseUrl_ReturnsError()
    {
        var error = await _plugin.InitAsync(new ClickHouseQueryConfig { BaseUrl = "ftp://127.0.0.1" });

        Assert.Contains("BaseUrl", error, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_ClampsFields()
    {
        var error = await _plugin.InitAsync(new ClickHouseQueryConfig
        {
            BaseUrl = "http://127.0.0.1:8123/",
            TimeoutSeconds = 9999,
            MaxRows = 0,
        });

        Assert.Null(error);
    }

    [Theory]
    [InlineData("UPDATE demo SET x = 1")]
    [InlineData("SYSTEM RELOAD CONFIG")]
    [InlineData("SELECT * FROM url('http://x')")]
    [InlineData("SELECT 1; DROP TABLE demo")]
    public async Task RunAsync_WriteOrExternalSource_RejectedBeforeConnection(string sql)
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ClickHouseQueryRequest { Sql = sql }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("只允许", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_EmptySql_RejectedReadable()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ClickHouseQueryRequest { Sql = "   " }, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("SQL 不能为空", exception.Message);
    }

    [Theory]
    [InlineData("SHOW CREATE TABLE otel.otel_traces")]
    [InlineData("SHOW DATABASES")]
    [InlineData("DESCRIBE TABLE otel.otel_logs")]
    public async Task RunAsync_DiscoverySql_PassesGuard(string sql)
    {
        // 摸库表/看结构语句能通过守卫进到执行层（客户端为 null，走到连接即抛 NRE 属预期外的成功路径标志）
        await Assert.ThrowsAnyAsync<NullReferenceException>(() => _plugin.RunAsync(new ClickHouseQueryRequest { Sql = sql }, CancellationToken.None));
    }
}
