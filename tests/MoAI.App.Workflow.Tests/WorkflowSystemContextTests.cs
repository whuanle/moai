using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// sys.* 系统上下文测试：调用方注入的 SystemContext（对话上下文）进入 sys 命名空间，
/// 引擎内置 currentTime 可用，上下文随实例持久化.
/// </summary>
public class WorkflowSystemContextTests
{
    [Fact]
    public async Task StartWithDefinitionAsync_WithSystemContext_ExposesSysVariables()
    {
        var definition = CreateEchoDefinition();
        var harness = new WorkflowTestHarness();

        var systemContext = new JsonObject
        {
            ["userId"] = "10001",
            ["appId"] = "6d0ec5e6-0000-0000-0000-000000000001",
            ["conversationId"] = "conv-1",
            ["messageId"] = "msg-1",
            ["history"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "你好" }),
        };

        var instance = await harness.Engine.StartWithDefinitionAsync(
            definition,
            new JsonObject { ["query"] = "你好" },
            systemContext: systemContext);

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var output = instance.NodeStates["echo"].Output!;
        Assert.Equal("10001", (string?)output["userId"]);
        Assert.Equal("conv-1", (string?)output["conversationId"]);
        Assert.Equal("msg-1", (string?)output["messageId"]);
        Assert.Equal(1, output["history"]!.AsArray().Count);
        Assert.Equal("你好", (string?)instance.Input["query"]);
    }

    [Fact]
    public async Task StartWithDefinitionAsync_CurrentTimeAlwaysAvailable()
    {
        var definition = CreateEchoDefinition();
        var harness = new WorkflowTestHarness();

        var instance = await harness.Engine.StartWithDefinitionAsync(
            definition,
            new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var currentTime = (string?)instance.NodeStates["echo"].Output!["currentTime"];
        Assert.False(string.IsNullOrWhiteSpace(currentTime));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", currentTime);
    }

    [Fact]
    public async Task SystemContext_PersistedWithInstance()
    {
        var definition = CreateEchoDefinition();
        var harness = new WorkflowTestHarness();

        var instance = await harness.Engine.StartWithDefinitionAsync(
            definition,
            new JsonObject { ["query"] = "q" },
            systemContext: new JsonObject { ["conversationId"] = "conv-42" });

        var stored = await harness.Store.FindInstanceByIdAsync(instance.Id);
        Assert.NotNull(stored);
        Assert.Equal("conv-42", (string?)stored!.SystemContext!["conversationId"]);
    }

    /// <summary>
    /// start → echo（JS 节点回显 sys 变量）→ end.
    /// </summary>
    private static WorkflowDefinition CreateEchoDefinition()
    {
        return new WorkflowDefinition
        {
            Id = "sys-echo",
            Name = "系统变量回显",
            Version = 1,
            Status = DefinitionStatus.Published,
            Nodes =
            [
                new NodeDefinition
                {
                    Key = "start",
                    Name = "开始",
                    Type = NodeTypes.Start,
                    Outputs = [new PortDefinition { Name = "query", FieldType = FieldType.String, IsRequired = true }],
                },
                new NodeDefinition
                {
                    Key = "echo",
                    Name = "回显",
                    Type = NodeTypes.JavaScript,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        code = "function run(inputs, sys) { return { userId: sys.userId, conversationId: sys.conversationId, messageId: sys.messageId, history: sys.history || [], currentTime: sys.currentTime } }",
                    }),
                    Outputs =
                    [
                        new PortDefinition { Name = "userId", FieldType = FieldType.String },
                        new PortDefinition { Name = "conversationId", FieldType = FieldType.String },
                        new PortDefinition { Name = "messageId", FieldType = FieldType.String },
                        new PortDefinition { Name = "history", FieldType = FieldType.Array },
                        new PortDefinition { Name = "currentTime", FieldType = FieldType.String },
                    ],
                },
                new NodeDefinition { Key = "end", Name = "结束", Type = NodeTypes.End },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "echo" },
                new ConnectionDefinition { Id = "c2", Source = "echo", Target = "end" },
            ],
        };
    }
}
