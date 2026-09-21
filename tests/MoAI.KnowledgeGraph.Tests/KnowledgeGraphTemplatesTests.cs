using MoAI.KnowledgeGraph.Models;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphTemplatesTests
{
    [Fact]
    public void All_ContainsBlankAndLogistics()
    {
        Assert.Contains(KnowledgeGraphTemplates.All, x => x.Key == KnowledgeGraphTemplates.BlankKey);
        Assert.Contains(KnowledgeGraphTemplates.All, x => x.Key == "logistics");
    }

    [Fact]
    public void Find_ReturnsTemplateOrNull()
    {
        var logistics = KnowledgeGraphTemplates.Find("logistics");
        Assert.NotNull(logistics);
        Assert.Contains(logistics!.EntityTypes, x => x.Name == "港口");
        Assert.Contains(logistics.EntityTypes, x => x.Name == "航段");
        Assert.Contains(logistics.EntityTypes, x => x.Name == "承运商");

        // 航段预置距离与运价属性，支撑 A 到 C 的最短距离/运价计算
        var leg = logistics.EntityTypes.Single(x => x.Name == "航段");
        Assert.Contains(leg.Properties, x => x.Name == "距离公里" && x.Type == KnowledgeGraphEntityTypeProperty.TypeNumber);
        Assert.Contains(leg.Properties, x => x.Name == "运输价格" && x.Type == KnowledgeGraphEntityTypeProperty.TypeNumber);

        Assert.Contains(logistics.RelationTypes, x => x.Name == "出发" && x.SourceType == "航段" && x.TargetType == "港口");
        Assert.Contains(logistics.RelationTypes, x => x.Name == "抵达" && x.SourceType == "航段" && x.TargetType == "港口");
        Assert.Null(KnowledgeGraphTemplates.Find("not-exist"));
        Assert.Null(KnowledgeGraphTemplates.Find(null));
    }
}
