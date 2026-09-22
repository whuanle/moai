using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// GraphSearchTextHelper 片段格式钉死：工作流 kgSearch 的 contents/text 与检索 API 的 Text 同源同形，
/// 格式变更必须两侧同步（头部含类型名、未知类型兜底、邻居行关系名兜底、截断标记）.
/// </summary>
public class GraphSearchTextHelperTests
{
    [Fact]
    public void BuildHitHeader_ContainsTypeName()
    {
        var hit = new GraphSearchHit(1, "n1", "张三", "MoAI 的作者", 3, "人物", 0.9, []);

        Assert.Equal("【张三（人物）】MoAI 的作者", GraphSearchTextHelper.BuildHitHeader(hit));
    }

    [Fact]
    public void BuildHitHeader_UnknownTypeFallback()
    {
        var hit = new GraphSearchHit(1, "n1", "张三", "MoAI 的作者", 0, null, 0.9, []);

        Assert.Equal("【张三（未知类型）】MoAI 的作者", GraphSearchTextHelper.BuildHitHeader(hit));
    }

    [Fact]
    public void BuildNeighborLine_UsesRelationName()
    {
        var line = GraphSearchTextHelper.BuildNeighborLine(new GraphNeighbor("认识", "out", "李四", "李四描述"));

        Assert.Equal("  └─ 认识(out)→ 李四：李四描述", line);
    }

    [Fact]
    public void BuildNeighborLine_MissingRelation_FallsBack()
    {
        var line = GraphSearchTextHelper.BuildNeighborLine(new GraphNeighbor(null, "in", "王五", "王五描述"));

        Assert.Equal("  └─ 关联(in)→ 王五：王五描述", line);
    }

    [Fact]
    public void BuildHitFragment_JoinsHeaderAndNeighborLines()
    {
        var hit = new GraphSearchHit(1, "n1", "张三", "MoAI 的作者", 3, "人物", 0.9,
            [new GraphNeighbor(null, "out", "李四", "李四描述"), new GraphNeighbor("指导", "in", "王五", "王五描述")]);

        var fragment = GraphSearchTextHelper.BuildHitFragment(hit, hit.Neighbors);

        Assert.Equal("【张三（人物）】MoAI 的作者\n  └─ 关联(out)→ 李四：李四描述\n  └─ 指导(in)→ 王五：王五描述", fragment);
    }

    [Fact]
    public void BuildHitFragment_NoNeighbors_HeaderOnly()
    {
        var hit = new GraphSearchHit(1, "n2", "MoAI", "开源项目", 0, null, null, []);

        Assert.Equal("【MoAI（未知类型）】开源项目", GraphSearchTextHelper.BuildHitFragment(hit, hit.Neighbors));
    }

    [Fact]
    public void Truncate_AppendsSuffix_WhenOverLimit()
    {
        var truncated = GraphSearchTextHelper.Truncate(new string('a', GraphSearchTextHelper.MaxTextLength + 1));

        Assert.Equal(GraphSearchTextHelper.MaxTextLength + "…(已截断)".Length, truncated.Length);
        Assert.EndsWith("…(已截断)", truncated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_KeepsShortText_AsIs()
    {
        Assert.Equal("短文本", GraphSearchTextHelper.Truncate("短文本"));
    }
}
