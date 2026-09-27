using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 智能运维观测插件共用 Authorization 头拼装测试.
/// </summary>
public class OpsAuthorizationTests
{
    [Fact]
    public void Build_NothingConfigured_ReturnsNull()
    {
        Assert.Null(OpsAuthorization.Build(null, null, null));
        Assert.Null(OpsAuthorization.Build(" ", " ", " "));
    }

    [Fact]
    public void Build_ApiKey_HighestPriority()
    {
        var header = OpsAuthorization.Build("admin", "pwd", "jwt-token", "VnNceEtleQ==");

        Assert.Equal("ApiKey VnNceEtleQ==", header);
    }

    [Fact]
    public void Build_Bearer_OverridesBasic()
    {
        Assert.Equal("Bearer jwt-token", OpsAuthorization.Build("admin", "pwd", " jwt-token "));
        Assert.Equal("Bearer jwt-token", OpsAuthorization.Build(null, null, "jwt-token"));
    }

    [Fact]
    public void Build_Basic_EncodesUsernameColonPassword()
    {
        var header = OpsAuthorization.Build("admin", "s3cret", null);

        Assert.Equal("Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:s3cret")), header);
    }

    [Fact]
    public void Build_Basic_NullPassword_EmptyPassword()
    {
        var header = OpsAuthorization.Build("admin", null, " ");

        Assert.Equal("Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:")), header);
    }
}
