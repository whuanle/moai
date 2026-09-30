using System;
using System.Text;
using MoAI.Infra.DingTalk;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 钉钉机器人加签计算测试（算法：base64(HMAC-SHA256(secret, "{ms}\n{secret}")) → URL 编码）.
/// </summary>
public class DingTalkSignHelperTests
{
    [Fact]
    public void Build_KnownSecretAndTime_MatchesManualHmac()
    {
        var secret = "SEC9f4a1b2c";
        var time = DateTimeOffset.FromUnixTimeMilliseconds(1790640000000L);

        var (timestamp, sign) = DingTalkSignHelper.Build(secret, time);

        Assert.Equal("1790640000000", timestamp);
        // 手工复算同一算法，校验 HMAC 数据串为 "{ms}\n{secret}"
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("1790640000000\nSEC9f4a1b2c")));
        Assert.Equal(Uri.EscapeDataString(expected), sign);
    }

    [Fact]
    public void Build_IsUrlEncoded()
    {
        var (_, sign) = DingTalkSignHelper.Build("SEC-e2e", DateTimeOffset.FromUnixTimeMilliseconds(1));

        Assert.DoesNotContain("+", sign, StringComparison.Ordinal);
        Assert.DoesNotContain("/", sign, StringComparison.Ordinal);
        Assert.DoesNotContain("=", sign, StringComparison.Ordinal);
    }
}
