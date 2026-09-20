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
/// AI 对话节点契约：config.aiModelId（兼容旧键 model）、config.systemPrompt、config.temperature、
/// config.skillIds/config.sandboxEnabled 透传、输入覆盖优先级.
/// </summary>
public class AiChatNodeTests
{
    private static (Mock<IAiChatClient> Client, AiChatNodeExecutor Executor) Create()
    {
        var client = new Mock<IAiChatClient>();
        client.Setup(c => c.CompleteAsync(
                It.IsAny<AiChatRequest>(),
                It.IsAny<Func<string, Task>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("回答");
        return (client, new AiChatNodeExecutor(client.Object));
    }

    private static NodeExecutionContext CreateContext(object? config, JsonObject inputs)
    {
        var node = new NodeDefinition
        {
            Key = "ai",
            Name = "AI 对话",
            Type = NodeTypes.AiChat,
            Config = config == null ? default : JsonSerializer.SerializeToElement(config),
        };
        return new NodeExecutionContext("inst-1", node, inputs, null!, new Mock<IWorkflowEventPublisher>().Object);
    }

    [Fact]
    public async Task Config_UsesAiModelId_AndPassesSystemPromptAndTemperature()
    {
        var (client, executor) = Create();
        var context = CreateContext(
            new { aiModelId = "model-id-1", systemPrompt = "你是严谨的助手", temperature = 0.7f },
            new JsonObject { ["prompt"] = "你好" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.Equal("回答", (string?)result.Output["answer"]);
        client.Verify(c => c.CompleteAsync(
            It.Is<AiChatRequest>(r =>
                r.SystemPrompt == "你是严谨的助手"
                && r.Prompt == "你好"
                && r.History == null
                && r.Model == "model-id-1"
                && r.Temperature == 0.7f),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Config_LegacyModelKey_StillResolves()
    {
        var (client, executor) = Create();
        var context = CreateContext(new { model = "legacy-model" }, new JsonObject { ["prompt"] = "你好" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        client.Verify(c => c.CompleteAsync(
            It.Is<AiChatRequest>(r => r.SystemPrompt == null && r.Prompt == "你好" && r.Model == "legacy-model" && r.Temperature == null),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Config_SkillIdsAndSandbox_PassThrough()
    {
        var (client, executor) = Create();
        var skill1 = Guid.NewGuid();
        var skill2 = Guid.NewGuid();
        var context = CreateContext(
            new { aiModelId = "m", skillIds = new[] { skill1.ToString(), skill2.ToString(), "not-a-guid", Guid.Empty.ToString() }, sandboxEnabled = true },
            new JsonObject { ["prompt"] = "执行技能" });

        await executor.ExecuteAsync(context, CancellationToken.None);

        client.Verify(c => c.CompleteAsync(
            It.Is<AiChatRequest>(r =>
                r.SkillIds.Count == 2
                && r.SkillIds.Contains(skill1)
                && r.SkillIds.Contains(skill2)
                && r.SandboxEnabled),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InputSystemOverridesConfigSystemPrompt_InputModelOverridesConfigModel()
    {
        var (client, executor) = Create();
        var context = CreateContext(
            new { aiModelId = "config-model", systemPrompt = "配置提示词" },
            new JsonObject
            {
                ["prompt"] = "你好",
                ["system"] = "输入提示词",
                ["model"] = "input-model",
            });

        await executor.ExecuteAsync(context, CancellationToken.None);

        client.Verify(c => c.CompleteAsync(
            It.Is<AiChatRequest>(r => r.SystemPrompt == "输入提示词" && r.Model == "input-model"),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Temperature_OutOfRangeOrString_IsNormalized()
    {
        var (client, executor) = Create();

        // 越界（>2）视为未设置
        await executor.ExecuteAsync(CreateContext(new { temperature = 3f }, new JsonObject { ["prompt"] = "a" }), CancellationToken.None);
        client.Verify(c => c.CompleteAsync(It.Is<AiChatRequest>(r => r.Temperature == null), It.IsAny<Func<string, Task>?>(), It.IsAny<CancellationToken>()), Times.Once);

        // 字符串形式的数字可解析
        await executor.ExecuteAsync(CreateContext(new { temperature = "0.5" }, new JsonObject { ["prompt"] = "b" }), CancellationToken.None);
        client.Verify(c => c.CompleteAsync(It.Is<AiChatRequest>(r => r.Temperature == 0.5f), It.IsAny<Func<string, Task>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MissingPrompt_Fails()
    {
        var (_, executor) = Create();
        var context = CreateContext(new { aiModelId = "m" }, new JsonObject());

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("prompt", result.ErrorMessage);
    }
}
