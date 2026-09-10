using MoAI.KnowledgeGraph.Models;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphTemplatesTests
{
    [Fact]
    public void All_ContainsBlankAndOps()
    {
        Assert.Contains(KnowledgeGraphTemplates.All, x => x.Key == KnowledgeGraphTemplates.BlankKey);
        Assert.Contains(KnowledgeGraphTemplates.All, x => x.Key == "ops");
    }

    [Fact]
    public void Find_ReturnsTemplateOrNull()
    {
        var ops = KnowledgeGraphTemplates.Find("ops");
        Assert.NotNull(ops);
        Assert.Contains("服务", ops!.EntityTypes);
        Assert.Contains(ops.RelationTypes, x => x.Name == "维护" && x.SourceType == "人员" && x.TargetType == "服务");
        Assert.Null(KnowledgeGraphTemplates.Find("not-exist"));
        Assert.Null(KnowledgeGraphTemplates.Find(null));
    }
}
