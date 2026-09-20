using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Nodes.Builtin;
using Moq;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// Agent 应用节点契约：config.agentAppId 必填、prompt/history 透传、输出 answer.
/// </summary>
public class AgentAppNodeTests
{
    private static (Mock<IWorkflowAgentAppClient> Client, AgentAppNodeExecutor Executor) Create()
    {
        var client = new Mock<IWorkflowAgentAppClient>();
        client.Setup(c => c.InvokeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<JsonArray?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Agent 回答");
        return (client, new AgentAppNodeExecutor(client.Object));
    }

    private static NodeExecutionContext CreateContext(object? config, JsonObject inputs)
    {
        var node = new NodeDefinition
        {
            Key = "agent",
            Name = "Agent 应用",
            Type = NodeTypes.AgentApp,
            Config = config == null ? default : JsonSerializer.SerializeToElement(config),
        };
        return new NodeExecutionContext("inst-1", node, inputs, null!, new Mock<IWorkflowEventPublisher>().Object);
    }

    [Fact]
    public async Task Invoke_WithPromptAndHistory_PassesAgentAppIdAndReturnsAnswer()
    {
        var (client, executor) = Create();
        var agentId = Guid.NewGuid();
        var history = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "先前问题" } };
        var context = CreateContext(new { agentAppId = agentId.ToString() }, new JsonObject
        {
            ["prompt"] = "帮我总结",
            ["history"] = history,
        });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.Equal("Agent 回答", (string?)result.Output["answer"]);
        client.Verify(c => c.InvokeAsync(agentId, "帮我总结", history, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MissingAgentAppId_Fails()
    {
        var (client, executor) = Create();
        var context = CreateContext(null, new JsonObject { ["prompt"] = "你好" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("未选择应用", result.ErrorMessage);
        client.Verify(c => c.InvokeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<JsonArray?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingPrompt_Fails()
    {
        var (_, executor) = Create();
        var context = CreateContext(new { agentAppId = Guid.NewGuid().ToString() }, new JsonObject());

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("prompt", result.ErrorMessage);
    }

    [Fact]
    public async Task PortFailure_BecomesNodeFailure()
    {
        var client = new Mock<IWorkflowAgentAppClient>();
        client.Setup(c => c.InvokeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<JsonArray?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkflowException("检测到循环嵌套"));
        var executor = new AgentAppNodeExecutor(client.Object);
        var context = CreateContext(new { agentAppId = Guid.NewGuid().ToString() }, new JsonObject { ["prompt"] = "你好" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("循环嵌套", result.ErrorMessage);
    }
}
