using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// SMTP 邮件发送插件参数校验行为测试（不发网络请求）.
/// </summary>
public class SmtpEmailSendPluginParamsTests
{
    private readonly SmtpEmailSendPlugin _plugin = new();

    public static TheoryData<SmtpEmailSendConfig> InvalidConfigs => new()
    {
        new SmtpEmailSendConfig { Host = "", Username = "a@x.com", Password = "p" },
        new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "", Password = "p" },
        new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "" },
        new SmtpEmailSendConfig { Host = "smtp.x.com", Port = 0, Username = "a@x.com", Password = "p" },
        new SmtpEmailSendConfig { Host = "smtp.x.com", Port = 70000, Username = "a@x.com", Password = "p" },
        new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "ops-bot", Password = "p" },
        new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p", Security = "tlsx" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public async Task InitAsync_InvalidConfig_ReturnsReadableError(SmtpEmailSendConfig config)
    {
        var error = await _plugin.InitAsync(config);

        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task InitAsync_ValidConfigWithFromName_Passes()
    {
        var error = await _plugin.InitAsync(new SmtpEmailSendConfig
        {
            Host = "smtp.x.com",
            Port = 465,
            Security = "ssl",
            Username = "a@x.com",
            Password = "p",
            FromName = "MoAI",
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task InitAsync_NonEmailUsername_RequiresFromAddress()
    {
        var error = await _plugin.InitAsync(new SmtpEmailSendConfig
        {
            Host = "smtp.x.com",
            Username = "ops-bot",
            Password = "p",
            FromAddress = "bot@x.com",
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_EmptyTo_RejectedBeforeNetwork()
    {
        await InitAsync(new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new SmtpEmailSendRequest
        {
            To = " , ;",
            Subject = "s",
            Body = "b",
        }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task RunAsync_InvalidAddress_RejectedBeforeNetwork()
    {
        await InitAsync(new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new SmtpEmailSendRequest
        {
            To = "a@x.com",
            Cc = "bad@@x.com",
            Subject = "s",
            Body = "b",
        }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Cc", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_EmptySubject_Rejected()
    {
        await InitAsync(new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new SmtpEmailSendRequest
        {
            To = "a@x.com",
            Subject = "  ",
            Body = "b",
        }, CancellationToken.None));

        Assert.Contains("Subject", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_EmptyBody_Rejected()
    {
        await InitAsync(new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new SmtpEmailSendRequest
        {
            To = "a@x.com",
            Subject = "s",
            Body = "",
        }, CancellationToken.None));

        Assert.Contains("Body", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_InvalidBase64Attachment_RejectedBeforeNetwork()
    {
        await InitAsync(new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new SmtpEmailSendRequest
        {
            To = "a@x.com",
            Subject = "s",
            Body = "b",
            Attachments = new() { new SmtpEmailAttachmentItem { FileName = "a.txt", ContentBase64 = "!!not-base64!!" } },
        }, CancellationToken.None));

        Assert.Contains("Base64", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PathInFileName_Sanitized()
    {
        // 文件名清洗属于前置校验：路径分隔符被剥离后才允许进入 MIME 组装，这里通过附件空名可观察（..\..\  清洗后为空 → 拒绝）.
        await InitAsync(new SmtpEmailSendConfig { Host = "smtp.x.com", Username = "a@x.com", Password = "p" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _plugin.RunAsync(new SmtpEmailSendRequest
        {
            To = "a@x.com",
            Subject = "s",
            Body = "b",
            Attachments = new() { new SmtpEmailAttachmentItem { FileName = "..\\..\\", ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("x")) } },
        }, CancellationToken.None));

        Assert.Contains("FileName", ex.Message, StringComparison.Ordinal);
    }

    private Task InitAsync(SmtpEmailSendConfig config) => _plugin.InitAsync(config);
}
