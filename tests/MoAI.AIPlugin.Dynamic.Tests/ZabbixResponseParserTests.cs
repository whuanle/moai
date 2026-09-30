using System.Text.Json;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Zabbix 响应解析测试.
/// </summary>
public class ZabbixResponseParserTests
{
    [Theory]
    [InlineData(0, "未分类")]
    [InlineData(1, "信息")]
    [InlineData(2, "警告")]
    [InlineData(3, "一般")]
    [InlineData(4, "严重")]
    [InlineData(5, "灾难")]
    public void SeverityName_KnownLevels_MapsChinese(int severity, string expected)
    {
        Assert.Equal(expected, ZabbixResponseParser.SeverityName(severity));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void SeverityName_OutOfRange_ReturnsUnknown(int severity)
    {
        Assert.Equal("未知", ZabbixResponseParser.SeverityName(severity));
    }

    [Fact]
    public void ExtractError_WithErrorObject_JoinsCodeMessageData()
    {
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","error":{"code":-32602,"message":"Invalid params.","data":"Login name or password is incorrect."},"id":1}""");

        var error = ZabbixResponseParser.ExtractError(document.RootElement);

        Assert.NotNull(error);
        Assert.Contains("-32602", error);
        Assert.Contains("Invalid params.", error);
        Assert.Contains("Login name or password is incorrect.", error);
    }

    [Fact]
    public void ExtractError_WithoutError_ReturnsNull()
    {
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","result":[],"id":1}""");

        Assert.Null(ZabbixResponseParser.ExtractError(document.RootElement));
    }

    [Fact]
    public void ClockToIso_UnixSeconds_FormatsUtcIso()
    {
        Assert.Equal("2025-01-01T00:00:00Z", ZabbixResponseParser.ClockToIso("1735689600"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void ClockToIso_Invalid_ReturnsEmpty(string clock)
    {
        Assert.Equal(string.Empty, ZabbixResponseParser.ClockToIso(clock));
    }

    [Fact]
    public void AgeFromClock_ThreeDaysAgo_ContainsDays()
    {
        var clock = DateTimeOffset.UtcNow.AddSeconds(-(3 * 86400) - (2 * 3600)).ToUnixTimeSeconds().ToString();

        var age = ZabbixResponseParser.AgeFromClock(clock);

        Assert.Contains("3d", age, StringComparison.Ordinal);
        Assert.Contains("2h", age, StringComparison.Ordinal);
    }

    [Fact]
    public void AgeFromClock_FutureOrInvalid_ReturnsEmpty()
    {
        var future = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString();

        Assert.Equal(string.Empty, ZabbixResponseParser.AgeFromClock(future));
        Assert.Equal(string.Empty, ZabbixResponseParser.AgeFromClock("abc"));
    }

    [Fact]
    public void ParseTags_TagValuePairs_BuildsDictionary()
    {
        using var document = JsonDocument.Parse("""[{"tag":"scope","value":"ops"},{"tag":"dc"},{"tag":"scope","value":"infra"}]""");

        var tags = ZabbixResponseParser.ParseTags(document.RootElement);

        Assert.Equal(2, tags.Count);
        Assert.Equal("infra", tags["scope"]);
        Assert.Equal(string.Empty, tags["dc"]);
    }

    [Fact]
    public void ParseTags_NotArray_ReturnsEmpty()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Empty(ZabbixResponseParser.ParseTags(document.RootElement));
    }
}
