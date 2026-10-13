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
/// 图片理解节点契约：config.aiModelId/config.temperature 透传、image/prompt 必填、
/// 温度越界归一、输出 answer 结构.
/// </summary>
public class ImageUnderstandingNodeTests
{
    private static (Mock<IWorkflowVisionClient> Client, ImageUnderstandingNodeExecutor Executor) Create()
    {
        var client = new Mock<IWorkflowVisionClient>();
        client.Setup(c => c.UnderstandAsync(
                It.IsAny<VisionRequest>(),
                It.IsAny<Func<string, Task>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("图片理解结果");
        return (client, new ImageUnderstandingNodeExecutor(client.Object));
    }

    private static NodeExecutionContext CreateContext(object? config, JsonObject inputs)
    {
        var node = new NodeDefinition
        {
            Key = "vision",
            Name = "图片理解",
            Type = NodeTypes.ImageUnderstanding,
            Config = config == null ? default : JsonSerializer.SerializeToElement(config),
        };
        return new NodeExecutionContext("inst-1", node, inputs, null!, new Mock<IWorkflowEventPublisher>().Object);
    }

    [Fact]
    public async Task Config_PassesModelAndTemperature_OutputAnswer()
    {
        var (client, executor) = Create();
        var context = CreateContext(
            new { aiModelId = "vision-model-1", temperature = 0.3f },
            new JsonObject { ["image"] = "https://example.com/a.png", ["prompt"] = "描述图片" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.Equal("图片理解结果", (string?)result.Output["answer"]);
        client.Verify(c => c.UnderstandAsync(
            It.Is<VisionRequest>(r => r.Model == "vision-model-1" && r.Temperature == 0.3f
                && r.Image == "https://example.com/a.png" && r.Prompt == "描述图片"),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Temperature_OutOfRange_IsNormalizedToNull()
    {
        var (client, executor) = Create();

        await executor.ExecuteAsync(
            CreateContext(new { aiModelId = "m", temperature = 3f }, new JsonObject { ["image"] = "https://e.com/a.png", ["prompt"] = "p" }),
            CancellationToken.None);
        client.Verify(c => c.UnderstandAsync(It.Is<VisionRequest>(r => r.Temperature == null), It.IsAny<Func<string, Task>?>(), It.IsAny<CancellationToken>()), Times.Once);

        await executor.ExecuteAsync(
            CreateContext(new { aiModelId = "m", temperature = "0.5" }, new JsonObject { ["image"] = "https://e.com/a.png", ["prompt"] = "p" }),
            CancellationToken.None);
        client.Verify(c => c.UnderstandAsync(It.Is<VisionRequest>(r => r.Temperature == 0.5f), It.IsAny<Func<string, Task>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MissingImage_Fails()
    {
        var (_, executor) = Create();
        var result = await executor.ExecuteAsync(CreateContext(new { aiModelId = "m" }, new JsonObject { ["prompt"] = "p" }), CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("image", result.ErrorMessage);
    }

    [Fact]
    public async Task EmptyImage_Fails()
    {
        var (_, executor) = Create();
        var result = await executor.ExecuteAsync(
            CreateContext(new { aiModelId = "m" }, new JsonObject { ["image"] = "", ["prompt"] = "p" }), CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("image", result.ErrorMessage);
    }

    [Fact]
    public async Task MissingPrompt_Fails()
    {
        var (_, executor) = Create();
        var result = await executor.ExecuteAsync(
            CreateContext(new { aiModelId = "m" }, new JsonObject { ["image"] = "https://e.com/a.png" }), CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("prompt", result.ErrorMessage);
    }

    [Fact]
    public async Task ClientThrows_FailsWithMessage()
    {
        var client = new Mock<IWorkflowVisionClient>();
        client.Setup(c => c.UnderstandAsync(It.IsAny<VisionRequest>(), It.IsAny<Func<string, Task>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("模型不存在"));
        var executor = new ImageUnderstandingNodeExecutor(client.Object);
        var result = await executor.ExecuteAsync(
            CreateContext(new { aiModelId = "m" }, new JsonObject { ["image"] = "https://e.com/a.png", ["prompt"] = "p" }),
            CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("模型不存在", result.ErrorMessage);
    }
}
