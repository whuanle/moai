using Xunit;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Tests;

public class WorkflowValidatorTests
{
    [Fact]
    public void Validate_DocQaDefinition_HasNoErrors()
    {
        var validator = new WorkflowValidator();
        var errors = validator.GetErrors(WorkflowTestHarness.CreateDocQaDefinition());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingStart_ReportsError()
    {
        var definition = WorkflowTestHarness.CreateDocQaDefinition();
        definition.Nodes.RemoveAll(n => n.Type == NodeTypes.Start);
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("开始节点"));
    }

    [Fact]
    public void Validate_DuplicateNodeKey_ReportsError()
    {
        var definition = WorkflowTestHarness.CreateDocQaDefinition();
        var clone = WorkflowJson.DeserializeDefinition(
            WorkflowJson.SerializeDefinition(WorkflowTestHarness.CreateDocQaDefinition())).Nodes[1];
        definition.Nodes.Add(clone);
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("Key 重复"));
    }

    [Fact]
    public void Validate_NormalNodeMultipleOutgoing_ReportsError()
    {
        var definition = new WorkflowDefinition
        {
            Id = "multi-out",
            Name = "多出边",
            Nodes =
            [
                new NodeDefinition { Key = "start", Name = "开始", Type = NodeTypes.Start },
                new NodeDefinition { Key = "js", Name = "脚本", Type = NodeTypes.JavaScript },
                new NodeDefinition { Key = "end1", Name = "结束1", Type = NodeTypes.End },
                new NodeDefinition { Key = "end2", Name = "结束2", Type = NodeTypes.End },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "js" },
                new ConnectionDefinition { Id = "c2", Source = "js", Target = "end1" },
                new ConnectionDefinition { Id = "c3", Source = "js", Target = "end2" },
            ],
        };
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("只允许一条输出连线"));
    }

    [Fact]
    public void Validate_ConditionMultipleOutgoing_Allowed()
    {
        var definition = new WorkflowDefinition
        {
            Id = "branch-out",
            Name = "条件多出边",
            Nodes =
            [
                new NodeDefinition { Key = "start", Name = "开始", Type = NodeTypes.Start },
                new NodeDefinition { Key = "check", Name = "条件", Type = NodeTypes.Condition },
                new NodeDefinition { Key = "end", Name = "结束", Type = NodeTypes.End },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "check" },
                new ConnectionDefinition { Id = "c2", Source = "check", Target = "end", Condition = "true" },
                new ConnectionDefinition { Id = "c3", Source = "check", Target = "end", Condition = "false" },
            ],
        };
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.DoesNotContain(errors, e => e.Contains("只允许一条输出连线"));
    }

    [Fact]
    public void Validate_Cycle_ReportsError()
    {
        var definition = new WorkflowDefinition
        {
            Id = "cycle",
            Name = "环",
            Nodes =
            [
                new NodeDefinition { Key = "start", Name = "开始", Type = NodeTypes.Start },
                new NodeDefinition { Key = "a", Name = "A", Type = NodeTypes.JavaScript },
                new NodeDefinition { Key = "end", Name = "结束", Type = NodeTypes.End },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "a" },
                new ConnectionDefinition { Id = "c2", Source = "a", Target = "a" },
                new ConnectionDefinition { Id = "c3", Source = "a", Target = "end" },
            ],
        };
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("环"));
    }

    [Fact]
    public void Validate_ConditionMissingFalseEdge_ReportsError()
    {
        var definition = WorkflowTestHarness.CreateDocQaDefinition();
        definition.Connections.RemoveAll(c => c.Id == "c5");
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("condition=false"));
    }

    [Fact]
    public void Validate_VariableReferencingNonAncestor_ReportsError()
    {
        var definition = WorkflowTestHarness.CreateDocQaDefinition();
        // start 节点引用下游 answer 节点的输出
        definition.Nodes[0].Inputs = new Dictionary<string, FieldBinding>
        {
            ["x"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "answer.answer" },
        };
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("非上游节点"));
    }
}
