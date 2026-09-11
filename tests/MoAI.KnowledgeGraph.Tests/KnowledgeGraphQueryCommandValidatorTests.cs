using FluentValidation;
using MoAI.KnowledgeGraph.Queries;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphQueryCommandValidatorTests
{
    [Fact]
    public void QuerySchema_Validate_RequiresPositiveKnowledgeGraphId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphSchemaCommand>();
        QueryKnowledgeGraphSchemaCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = 0 }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = 1 }).IsValid);
    }

    [Fact]
    public void QueryNodes_Validate_RequiresPositiveKnowledgeGraphId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphNodesCommand>();
        QueryKnowledgeGraphNodesCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphNodesCommand { KnowledgeGraphId = 0 }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphNodesCommand { KnowledgeGraphId = 1 }).IsValid);
    }

    [Fact]
    public void QueryEdges_Validate_RequiresPositiveKnowledgeGraphId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphEdgesCommand>();
        QueryKnowledgeGraphEdgesCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphEdgesCommand { KnowledgeGraphId = 0 }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphEdgesCommand { KnowledgeGraphId = 1 }).IsValid);
    }

    [Fact]
    public void QueryNode_Validate_RequiresKnowledgeGraphIdAndNodeId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphNodeCommand>();
        QueryKnowledgeGraphNodeCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphNodeCommand { KnowledgeGraphId = 0, NodeId = "n1" }).IsValid);
        Assert.False(validator.Validate(new QueryKnowledgeGraphNodeCommand { KnowledgeGraphId = 1, NodeId = string.Empty }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphNodeCommand { KnowledgeGraphId = 1, NodeId = "n1" }).IsValid);
    }

    [Fact]
    public void QueryEdge_Validate_RequiresKnowledgeGraphIdAndEdgeId()
    {
        var validator = new InlineValidator<QueryKnowledgeGraphEdgeCommand>();
        QueryKnowledgeGraphEdgeCommand.Validate(validator);

        Assert.False(validator.Validate(new QueryKnowledgeGraphEdgeCommand { KnowledgeGraphId = 0, EdgeId = "e1" }).IsValid);
        Assert.False(validator.Validate(new QueryKnowledgeGraphEdgeCommand { KnowledgeGraphId = 1, EdgeId = string.Empty }).IsValid);
        Assert.True(validator.Validate(new QueryKnowledgeGraphEdgeCommand { KnowledgeGraphId = 1, EdgeId = "e1" }).IsValid);
    }
}
