using MoAI.AIPlugin.Dynamic.HotBoard;
using MoAI.AIPlugin.Dynamic.HotBoard.Channels;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 热榜渠道目录归一化测试（别名/去重/未知来源/构造）.
/// </summary>
public class HotBoardCatalogTests
{
    [Fact]
    public void TryNormalize_MapsAliasesAndDeduplicates()
    {
        var ok = HotBoardCatalog.TryNormalize(
            new[] { "知乎", " ZHIHU ", "微博", "b站", "稀土掘金", "腾讯新闻" },
            out var ids,
            out var unknown);

        Assert.True(ok);
        Assert.Equal(new[] { "zhihu", "weibo", "bilibili", "juejin", "qq-news" }, ids);
        Assert.Empty(unknown);
    }

    [Fact]
    public void TryNormalize_UnknownInputsCollected()
    {
        var ok = HotBoardCatalog.TryNormalize(new[] { "知乎", "抖音号" }, out var ids, out var unknown);

        Assert.True(ok);
        Assert.Equal(new[] { "zhihu" }, ids);
        Assert.Equal(new[] { "抖音号" }, unknown);
    }

    [Fact]
    public void TryNormalize_EmptyOrNull_ReturnsFalse()
    {
        Assert.False(HotBoardCatalog.TryNormalize(null, out _, out _));
        Assert.False(HotBoardCatalog.TryNormalize(new[] { "  ", "" }, out _, out var unknown));
        Assert.Empty(unknown);
    }

    [Fact]
    public void Ids_ContainsAllSixteenPlatforms()
    {
        Assert.Equal(16, HotBoardCatalog.Ids.Count);
        foreach (var expected in new[] { "36kr", "zhihu", "weibo", "juejin", "toutiao", "baidu", "bilibili", "douyin", "ithome", "hupu", "huxiu", "thepaper", "geekpark", "qq-news", "netease-news", "sina-news" })
        {
            Assert.Contains(expected, HotBoardCatalog.Ids);
        }

        Assert.Contains("36kr", HotBoardCatalog.DescribeIds(), StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayNameOf_MapsChineseNames()
    {
        Assert.Equal("知乎", HotBoardCatalog.DisplayNameOf("zhihu"));
        Assert.Equal("36氪", HotBoardCatalog.DisplayNameOf("36kr"));
        Assert.Equal("稀土掘金", HotBoardCatalog.DisplayNameOf("juejin"));
        Assert.Equal("what", HotBoardCatalog.DisplayNameOf("what"));
    }

    [Fact]
    public void Create_BuildsChannelByType()
    {
        using var client = new HttpClient();

        Assert.IsType<ZhihuChannel>(HotBoardCatalog.Create("zhihu", client, null));
        Assert.IsType<JuejinChannel>(HotBoardCatalog.Create("juejin", client, "http://127.0.0.1:5280"));
        Assert.Throws<ArgumentException>(() => HotBoardCatalog.Create("nope", client, null));
    }
}
