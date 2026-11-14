using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class GraphSearchTextHelperTests
{
    private static GraphSearchHit MakeHit(string name, int neighborCount, int descriptionPadding = 0)
    {
        var neighbors = Enumerable.Range(0, neighborCount)
            .Select(i => new GraphNeighbor("关系", "out", $"邻居{i}", "描述" + new string('x', descriptionPadding), i))
            .ToList();
        return new GraphSearchHit(1, $"node-{name}", name, "描述", 1, "类型", 0.9, neighbors);
    }

    [Fact]
    public void BuildHitsText_UnderBudget_KeepsAllFragments()
    {
        var hits = new List<GraphSearchHit> { MakeHit("甲", 2), MakeHit("乙", 1) };
        var (text, truncated) = GraphSearchTextHelper.BuildHitsText(hits);

        Assert.False(truncated);
        Assert.Contains("【甲（类型）】描述", text);
        Assert.Contains("【乙（类型）】描述", text);
        // 段间以空行分隔
        Assert.Contains("\n\n", text);
    }

    [Fact]
    public void BuildHitsText_OverBudget_DropsTailFragments_AndMarksTruncated()
    {
        // 单段约 8200+ 字符（超 8192 预算）：第一段自身填满预算 → 第二段整段被裁
        var big = MakeHit("大段", 3, descriptionPadding: 8000);
        var tail = MakeHit("尾部", 1);
        var (text, truncated) = GraphSearchTextHelper.BuildHitsText([big, tail]);

        Assert.True(truncated);
        Assert.Contains("大段", text);
        Assert.DoesNotContain("尾部", text);
    }

    [Fact]
    public void BuildHitsText_SingleOversizedFragment_FallsBackToPrefixTruncation()
    {
        var huge = MakeHit("巨段", 3, descriptionPadding: 9000);
        var (text, truncated) = GraphSearchTextHelper.BuildHitsText([huge]);

        Assert.True(truncated);
        Assert.True(text.Length <= GraphSearchTextHelper.MaxTextLength + "…(已截断)".Length);
        Assert.EndsWith("…(已截断)", text, StringComparison.Ordinal);
    }
}
