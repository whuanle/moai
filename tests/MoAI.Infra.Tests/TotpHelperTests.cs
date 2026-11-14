using System.Text;
using MoAI.Infra.Helpers;
using Xunit;

namespace MoAI.Infra.Tests;

/// <summary>
/// <see cref="TotpHelper"/> 单元测试：向量取自 RFC 6238 附录 B（8 位口令取模 10^6 即 6 位口令）.
/// </summary>
public class TotpHelperTests
{
    // ASCII "12345678901234567890" 的标准 base32 编码（RFC 6238 测试密钥）
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    public static TheoryData<long, string> Rfc6238Vectors => new()
    {
        // T（Unix 秒）→ 6 位口令
        { 59, "287082" },
        { 1111111109, "081804" },
        { 1111111111, "050471" },
        { 1234567890, "005924" },
        { 2000000000, "279037" },
        { 20000000000, "353130" },
    };

    [Theory]
    [MemberData(nameof(Rfc6238Vectors))]
    public void ComputeCode_MatchesRfc6238Vectors(long unixTimeSeconds, string expected)
    {
        var timeStep = unixTimeSeconds / 30;
        Assert.Equal(expected, TotpHelper.ComputeCode(RfcSecret, timeStep));
    }

    [Fact]
    public void TryVerifyCode_AcceptsCurrentAndAdjacentSteps()
    {
        long now = 1111111109;
        long step = now / 30;

        // 当前步长命中
        Assert.True(TotpHelper.TryVerifyCode(RfcSecret, "081804", out var matched, now));
        Assert.Equal(step, matched);

        // 前/后一个步长在容差窗口内
        Assert.True(TotpHelper.TryVerifyCode(RfcSecret, TotpHelper.ComputeCode(RfcSecret, step - 1), out _, now));
        Assert.True(TotpHelper.TryVerifyCode(RfcSecret, TotpHelper.ComputeCode(RfcSecret, step + 1), out _, now));

        // 窗口外（-2 步长）不通过
        Assert.False(TotpHelper.TryVerifyCode(RfcSecret, TotpHelper.ComputeCode(RfcSecret, step - 2), out _, now));
    }

    [Fact]
    public void TryVerifyCode_RejectsBadInput()
    {
        long now = 1111111109;

        Assert.False(TotpHelper.TryVerifyCode(RfcSecret, string.Empty, out _, now));
        Assert.False(TotpHelper.TryVerifyCode(RfcSecret, "12345", out _, now));
        Assert.False(TotpHelper.TryVerifyCode(RfcSecret, "1234567", out _, now));
        Assert.False(TotpHelper.TryVerifyCode(RfcSecret, "abcdef", out _, now));
        Assert.False(TotpHelper.TryVerifyCode(RfcSecret, null!, out _, now));
        Assert.False(TotpHelper.TryVerifyCode(string.Empty, "081804", out _, now));
    }

    [Fact]
    public void Base32EncodeDecode_RoundTrip()
    {
        Assert.Equal(string.Empty, TotpHelper.Base32Encode(Array.Empty<byte>()));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", TotpHelper.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890")));
        Assert.Equal("12345678901234567890", Encoding.ASCII.GetString(TotpHelper.Base32Decode("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ")));

        // 随机密钥往返一致；解码容忍小写与填充符
        var secret = TotpHelper.GenerateSecret();
        Assert.Equal(32, secret.Length);
        var bytes = TotpHelper.Base32Decode(secret);
        Assert.Equal(secret, TotpHelper.Base32Encode(bytes));
        var lowered = string.Concat(secret.Select(c => char.IsAsciiLetter(c) ? (char)(c | 0x20) : c));
        Assert.Equal(bytes, TotpHelper.Base32Decode(lowered + "===="));
    }

    [Fact]
    public void BuildOtpauthUri_EscapesIssuerAndAccount()
    {
        var uri = TotpHelper.BuildOtpauthUri("ABC234DEF", "a@b.com", "Mo AI");
        Assert.Equal("otpauth://totp/Mo%20AI:a%40b.com?secret=ABC234DEF&issuer=Mo%20AI&digits=6&period=30&algorithm=SHA1", uri);
    }
}
