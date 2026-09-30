using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// SSH 命令白名单守卫测试.
/// </summary>
public class SshCommandGuardTests
{
    private const string Whitelist = "systemctl status,journalctl,df,free,uptime,tail,ps,grep";

    [Theory]
    [InlineData("uptime")]
    [InlineData("df -h")]
    [InlineData("systemctl status nginx")]
    [InlineData("journalctl -u nginx --since \"1 hour ago\" | tail -n 100")]
    [InlineData("ps aux | grep nginx | grep -v grep")]
    public void Validate_Whitelisted_ReturnsNull(string command)
    {
        Assert.Null(SshCommandGuard.Validate(command, Whitelist));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyCommand_Rejected(string? command)
    {
        Assert.NotNull(SshCommandGuard.Validate(command, Whitelist));
    }

    [Fact]
    public void Validate_EmptyWhitelist_FailClosed()
    {
        var error = SshCommandGuard.Validate("uptime", "");

        Assert.NotNull(error);
        Assert.Contains("白名单", error);
    }

    [Theory]
    [InlineData("df -h; rm -rf /")]
    [InlineData("uptime && reboot")]
    [InlineData("df -h || shutdown")]
    [InlineData("tail -f /var/log/syslog & cat /etc/shadow")]
    [InlineData("echo `whoami`")]
    [InlineData("echo $(cat /etc/passwd)")]
    [InlineData("uptime\ncat /etc/shadow")]
    public void Validate_ChainingOrSubstitution_Rejected(string command)
    {
        var error = SshCommandGuard.Validate(command, Whitelist);

        Assert.NotNull(error);
        Assert.Contains("拼接/替换符号", error);
    }

    [Theory]
    [InlineData("mkfs.ext4 /dev/sda1")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("reboot")]
    [InlineData("shutdown -h now")]
    [InlineData("fdisk -l")]
    public void Validate_ForbiddenCommands_RejectedEvenIfWhitelisted(string command)
    {
        var error = SshCommandGuard.Validate(command, Whitelist);

        Assert.NotNull(error);
        Assert.Contains("灾难级黑名单", error);
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -fr /*")]
    [InlineData("rm --recursive -f / ")]
    public void Validate_CatastrophicRm_RejectedEvenIfWhitelisted(string command)
    {
        var error = SshCommandGuard.Validate(command, "rm");

        Assert.NotNull(error);
        Assert.Contains("灾难级递归删除", error);
    }

    [Fact]
    public void Validate_OrdinaryRm_WhitelistedAllows()
    {
        // rm 交给实例白名单管理：普通目录清理放行，根/根通配由灾难级递归删除检查兜底.
        Assert.Null(SshCommandGuard.Validate("rm -rf /tmp/ops-cache", "rm"));
    }

    [Fact]
    public void Validate_EveryPipeSegmentMustMatch()
    {
        // 第二段 grep 在白名单内 → 放行；第二段未在白名单 → 拒绝
        Assert.Null(SshCommandGuard.Validate("ps aux | grep nginx", Whitelist));
        Assert.NotNull(SshCommandGuard.Validate("ps aux | cat /etc/shadow", Whitelist));
    }

    [Fact]
    public void Validate_PrefixMustRespectTokenBoundary()
    {
        Assert.NotNull(SshCommandGuard.Validate("dfx", Whitelist));
        Assert.Null(SshCommandGuard.Validate("df -h", Whitelist));
    }

    [Fact]
    public void Validate_UnknownCommand_ReportsSegment()
    {
        var error = SshCommandGuard.Validate("docker ps", Whitelist);

        Assert.NotNull(error);
        Assert.Contains("docker ps", error);
        Assert.Contains("未命中", error);
    }
}
