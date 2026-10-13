using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// IMAP 邮箱读取插件参数校验行为测试（不发网络请求）.
/// </summary>
public class ImapEmailQueryPluginParamsTests
{
    private readonly ImapEmailQueryPlugin _plugin = new();

    public static TheoryData<ImapEmailQueryConfig> InvalidConfigs => new()
    {
        new ImapEmailQueryConfig { Host = "", Username = "a@x.com", Password = "p" },
        new ImapEmailQueryConfig { Host = "imap.x.com", Username = "", Password = "p" },
        new ImapEmailQueryConfig { Host = "imap.x.com", Username = "a@x.com", Password = "" },
        new ImapEmailQueryConfig { Host = "imap.x.com", Port = -1, Username = "a@x.com", Password = "p" },
        new ImapEmailQueryConfig { Host = "imap.x.com", Username = "a@x.com", Password = "p", Security = "implicit" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public async Task InitAsync_InvalidConfig_ReturnsReadableError(ImapEmailQueryConfig config)
    {
        var error = await _plugin.InitAsync(config);

        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task InitAsync_ValidConfig_Passes()
    {
        var error = await _plugin.InitAsync(new ImapEmailQueryConfig
        {
            Host = "imap.x.com",
            Port = 993,
            Username = "a@x.com",
            Password = "p",
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task InitAsync_BlankFolder_FallsBackToInbox()
    {
        var error = await _plugin.InitAsync(new ImapEmailQueryConfig
        {
            Host = "imap.x.com",
            Username = "a@x.com",
            Password = "p",
            Folder = "  ",
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_UnknownMode_RejectedReadable()
    {
        await InitValidAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ImapEmailQueryRequest
        {
            Mode = "delete_all",
        }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("delete_all", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SearchWithoutCriteria_RejectedBeforeNetwork()
    {
        await InitValidAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ImapEmailQueryRequest
        {
            Mode = "search",
        }, CancellationToken.None));

        Assert.Contains("至少需要一个过滤条件", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DetailWithoutUid_RejectedBeforeNetwork()
    {
        await InitValidAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ImapEmailQueryRequest
        {
            Mode = "detail",
        }, CancellationToken.None));

        Assert.Contains("Uid", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MarkReadWithoutUid_RejectedBeforeNetwork()
    {
        await InitValidAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ImapEmailQueryRequest
        {
            Mode = "mark_read",
        }, CancellationToken.None));

        Assert.Contains("Uid", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_InvalidSinceDate_RejectedBeforeNetwork()
    {
        await InitValidAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new ImapEmailQueryRequest
        {
            Mode = "search",
            SinceDate = "2026/01/06",
        }, CancellationToken.None));

        Assert.Contains("yyyy-MM-dd", ex.Message, StringComparison.Ordinal);
    }

    private Task InitValidAsync() => _plugin.InitAsync(new ImapEmailQueryConfig
    {
        Host = "imap.x.com",
        Username = "a@x.com",
        Password = "p",
    });
}
