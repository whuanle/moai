using System.Net;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 拨测插件内网防护测试.
/// </summary>
public class HttpProbeGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.10.20")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("0.1.2.3")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void IsPrivateAddress_Private_ReturnsTrue(string address)
    {
        Assert.True(HttpProbeGuard.IsPrivateAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("11.0.0.1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:8.8.8.8")]
    public void IsPrivateAddress_Public_ReturnsFalse(string address)
    {
        Assert.False(HttpProbeGuard.IsPrivateAddress(IPAddress.Parse(address)));
    }
}
