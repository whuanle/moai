using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Redis 诊断响应解析测试.
/// </summary>
public class RedisResponseParserTests
{
    [Fact]
    public void ParseInfo_TwoSections_GroupsPairs()
    {
        var raw = "# Server\r\nredis_version:7.2.4\r\nredis_mode:standalone\r\n\r\n# Memory\r\nused_memory:104857600\r\nmaxmemory:0\r\n";

        var sections = RedisResponseParser.ParseInfo(raw);

        Assert.Equal("7.2.4", sections["server"]["redis_version"]);
        Assert.Equal("standalone", sections["server"]["redis_mode"]);
        Assert.Equal("104857600", sections["memory"]["used_memory"]);
        Assert.Equal("0", sections["memory"]["maxmemory"]);
    }

    [Fact]
    public void ParseInfo_LinesWithoutColon_Skipped()
    {
        var raw = "# Memory\r\nbroken line\r\nused_memory:1\r\n";

        var sections = RedisResponseParser.ParseInfo(raw);

        Assert.Single(sections["memory"]);
    }

    [Fact]
    public void ParseClientList_TwoClients_ParsesKeyValuePairs()
    {
        var raw = "id=11 addr=127.0.0.1:50001 name=ops-e2e db=0\r\nid=12 addr=127.0.0.1:50002 name= db=1\r\n";

        var clients = RedisResponseParser.ParseClientList(raw);

        Assert.Equal(2, clients.Count);
        Assert.Equal("127.0.0.1:50001", clients[0]["addr"]);
        Assert.Equal("ops-e2e", clients[0]["name"]);
        Assert.Equal(string.Empty, clients[1]["name"]);
        Assert.Equal("1", clients[1]["db"]);
    }

    [Fact]
    public void BuildSlowLogEntry_FormatsTimestampAndArgs()
    {
        var entry = RedisResponseParser.BuildSlowLogEntry(7, 1735689600, 15000, "GET", "bigkey", "127.0.0.1:50001", "ops-e2e");

        Assert.Equal(7, entry.Id);
        Assert.Equal("2025-01-01T00:00:00Z", entry.Timestamp);
        Assert.Equal(15000, entry.DurationMicros);
        Assert.Equal("GET", entry.Command);
        Assert.Equal("bigkey", entry.Args);
        Assert.Equal("127.0.0.1:50001", entry.ClientAddress);
        Assert.Equal("ops-e2e", entry.ClientName);
    }

    [Fact]
    public void BuildSlowLogEntry_LongArgs_Truncated()
    {
        var args = new string('x', 300);

        var entry = RedisResponseParser.BuildSlowLogEntry(1, 0, 1, "SET", args, string.Empty, string.Empty);

        Assert.True(entry.Args.Length < 300);
        Assert.EndsWith("...", entry.Args);
    }

    [Theory]
    [InlineData("string", "STRLEN")]
    [InlineData("hash", "HLEN")]
    [InlineData("list", "LLEN")]
    [InlineData("set", "SCARD")]
    [InlineData("zset", "ZCARD")]
    [InlineData("stream", "XLEN")]
    public void LengthCommandFor_TypedKeys_MapsCommands(string type, string expected)
    {
        Assert.Equal(expected, RedisResponseParser.LengthCommandFor(type));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("module")]
    [InlineData("unknown")]
    public void LengthCommandFor_Unapplicable_ReturnsNull(string type)
    {
        Assert.Null(RedisResponseParser.LengthCommandFor(type));
    }
}
