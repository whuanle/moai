using System;
using MoAI.App;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// 应用版本号 aaa.bb.cc 与 int 编码（aaa*10000 + bb*100 + cc）互转规则.
/// </summary>
public class AppVersionHelperTests
{
    [Theory]
    [InlineData("1.0.0", 10000)]
    [InlineData("1.2.3", 10203)]
    [InlineData("0.0.1", 1)]
    [InlineData("10.20.30", 102030)]
    [InlineData("999.99.99", 9999999)]
    [InlineData("01.02.03", 10203)]
    public void TryParse_ValidText_EncodesAsInt(string text, int expected)
    {
        Assert.True(AppVersionHelper.TryParse(text, out var version));
        Assert.Equal(expected, version);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.333")]
    [InlineData("1234.1.1")]
    [InlineData("100.0.100")]
    [InlineData("-1.0.0")]
    [InlineData("1.-2.3")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a.b.c")]
    [InlineData("1.2.x")]
    public void TryParse_InvalidText_ReturnsFalse(string text)
    {
        Assert.False(AppVersionHelper.TryParse(text, out _));
    }

    [Theory]
    [InlineData(10000, "1.0.0")]
    [InlineData(10203, "1.2.3")]
    [InlineData(1, "0.0.1")]
    [InlineData(102030, "10.20.30")]
    [InlineData(9999999, "999.99.99")]
    public void Format_Encoding_RendersAaaBbCc(int version, string expected)
    {
        Assert.Equal(expected, AppVersionHelper.Format(version));
    }

    [Fact]
    public void Encoding_PreservesOrderForComparison()
    {
        // int 编码的核心目的：跨段比较与个位数进位不串位（1.10.0 > 1.9.0）
        Assert.True(AppVersionHelper.TryParse("1.10.0", out var v110));
        Assert.True(AppVersionHelper.TryParse("1.9.0", out var v19));
        Assert.True(AppVersionHelper.TryParse("2.0.0", out var v200));
        Assert.True(v110 > v19);
        Assert.True(v200 > v110);
    }

    [Fact]
    public void TryParse_Format_RoundTrips()
    {
        for (var aaa = 0; aaa <= 999; aaa += 137)
        {
            for (var bb = 0; bb <= 99; bb += 17)
            {
                for (var cc = 0; cc <= 99; cc += 19)
                {
                    var text = $"{aaa}.{bb}.{cc}";
                    Assert.True(AppVersionHelper.TryParse(text, out var version));
                    Assert.Equal(text, AppVersionHelper.Format(version));
                }
            }
        }
    }
}
