using FluentValidation;
using MoAI.KnowledgeGraph.Queries;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphQueryCommandValidatorTests
{
    // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，
    // 因此各 Validate 只校验请求体字段（与 wiki 等模块约定一致），不对路由字段断言。

    [Fact]
    public void QuerySchema_Validate_PassesForAnyRouteBoundId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphSchemaCommand>();
        QueryKnowledgeGraphSchemaCommand.Validate(validator);

        Assert.True(validator.Validate(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = 1 }).IsValid);
    }

    [Fact]
    public void QueryNodes_Validate_ClampsPagingFieldsOnly()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphNodesCommand>();
        QueryKnowledgeGraphNodesCommand.Validate(validator);

        Assert.True(validator.Validate(new QueryKnowledgeGraphNodesCommand { KnowledgeGraphId = 1 }).IsValid);
    }

    [Fact]
    public void QueryEdges_Validate_PassesForAnyRouteBoundId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphEdgesCommand>();
        QueryKnowledgeGraphEdgesCommand.Validate(validator);

        Assert.True(validator.Validate(new QueryKnowledgeGraphEdgesCommand { KnowledgeGraphId = 1 }).IsValid);
    }

    [Fact]
    public void QueryNode_Validate_RequiresNodeIdOnly()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphNodeCommand>();
        QueryKnowledgeGraphNodeCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphNodeCommand { KnowledgeGraphId = 1, NodeId = string.Empty }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphNodeCommand { KnowledgeGraphId = 1, NodeId = "n1" }).IsValid);
    }

    [Fact]
    public void QueryEdge_Validate_RequiresEdgeIdOnly()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphEdgeCommand>();
        QueryKnowledgeGraphEdgeCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphEdgeCommand { KnowledgeGraphId = 1, EdgeId = string.Empty }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphEdgeCommand { KnowledgeGraphId = 1, EdgeId = "e1" }).IsValid);
    }

    [Fact]
    public void QuerySearch_Validate_RejectsOutOfRangeContractValues()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphSearchCommand>();
        QueryKnowledgeGraphSearchCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = string.Empty, TopK = 5 }).IsValid);
        Assert.False(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", TopK = 0 }).IsValid);
        Assert.False(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", TopK = 51 }).IsValid);
        Assert.False(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", TopK = 5, MinScore = 1.5 }).IsValid);
    }

    [Fact]
    public void QuerySearch_Validate_DefaultsAndInclusiveBoundsPass()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphSearchCommand>();
        QueryKnowledgeGraphSearchCommand.Validate(validator);

        // 默认值契约：TopK=5、MinScore=null
        Assert.True(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询" }).IsValid);

        // 数值契约边界（Kiota 客户端会固化）：TopK 与 MinScore 均为闭区间
        Assert.True(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", TopK = 1 }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", TopK = 50 }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", MinScore = 0 }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = 1, Query = "查询", MinScore = 1 }).IsValid);
    }
}
