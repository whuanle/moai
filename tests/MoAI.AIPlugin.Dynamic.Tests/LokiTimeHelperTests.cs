using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Loki 时间参数归一测试.
/// </summary>
public class LokiTimeHelperTests
{
    [Fact]
    public void ToNanoSeconds_Rfc3339_ConvertsToNanoseconds()
    {
        // 2026-09-29T00:00:00Z = 1790640000 秒 = 1790640000000 毫秒 = 1790640000000000000 纳秒
        Assert.Equal("1790640000000000000", LokiTimeHelper.ToNanoSeconds("2026-09-29T00:00:00Z"));
    }

    [Theory]
    [InlineData("1790640000", "1790640000000000000")]
    [InlineData("1790640000000", "1790640000000000000")]
    [InlineData("1790640000000000000", "1790640000000000000")]
    public void ToNanoSeconds_NumericScales_ConvertsToNanoseconds(string input, string expected)
    {
        Assert.Equal(expected, LokiTimeHelper.ToNanoSeconds(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void ToNanoSeconds_Invalid_ReturnsNull(string? value)
    {
        Assert.Null(LokiTimeHelper.ToNanoSeconds(value));
    }

    [Fact]
    public void NanoSecondsToIso_Valid_ConvertsToIso()
    {
        Assert.Equal("2026-09-29T00:00:00Z", LokiTimeHelper.NanoSecondsToIso("1790640000000000000"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("abc")]
    public void NanoSecondsToIso_Invalid_ReturnsEmpty(string? value)
    {
        Assert.Equal(string.Empty, LokiTimeHelper.NanoSecondsToIso(value));
    }
}
