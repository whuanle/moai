using Moq;
using Xunit;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Nodes;

namespace MoAI.App.Workflow.Tests;

public class WorkflowEngineExecutionTests
{
    private static async Task<WorkflowTestHarness> PrepareAsync()
    {
        var harness = new WorkflowTestHarness();
        await harness.Store.SaveDefinitionAsync(WorkflowTestHarness.CreateDocQaDefinition());
        harness.AiChat
            .Setup(c => c.CompleteAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<JsonArray?>(),
                It.IsAny<string?>(),
                It.IsAny<Func<string, Task>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("根据资料回答内容");
        return harness;
    }

    private static void SetupKnowledgeSearch(WorkflowTestHarness harness, bool hasResult)
    {
        harness.PluginInvoker
            .Setup(i => i.InvokeAsync("mock.knowledgeSearch", It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonObject
            {
                ["documents"] = new JsonArray { new JsonObject { ["title"] = "MoAI 文档" } },
                ["hasResult"] = hasResult,
            });
        harness.PluginInvoker
            .Setup(i => i.InvokeAsync("mock.fallback", It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonObject { ["answer"] = "抱歉，没有找到相关资料。" });
    }

    [Fact]
    public async Task StartAsync_TrueBranch_CompletesWithAiAnswer()
    {
        var harness = await PrepareAsync();
        SetupKnowledgeSearch(harness, hasResult: true);

        var instance = await harness.Engine.StartAsync("doc-qa", new JsonObject { ["query"] = "什么是工作流？" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        Assert.Equal("根据资料回答内容", (string?)instance.Output!["answer"]);
        Assert.Null((string?)instance.Output!["fallbackAnswer"]);
        Assert.Equal(NodeState.Completed, instance.NodeStates["search"].State);
        Assert.Equal(NodeState.Completed, instance.NodeStates["answer"].State);
        Assert.Equal(NodeState.Skipped, instance.NodeStates["fallback"].State);
        Assert.Equal(NodeState.Completed, instance.NodeStates["end"].State);

        // 插值表达式已解析上游输出
        harness.AiChat.Verify(c => c.CompleteAsync(
            "你是问答助手。",
            "用户问题：什么是工作流？\n参考资料摘要：MoAI 文档",
            It.IsAny<JsonArray?>(),
            "mock-gpt-4o",
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_FalseBranch_RunsFallbackAndSkipsAi()
    {
        var harness = await PrepareAsync();
        SetupKnowledgeSearch(harness, hasResult: false);

        var instance = await harness.Engine.StartAsync("doc-qa", new JsonObject { ["query"] = "问题" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        Assert.Equal("抱歉，没有找到相关资料。", (string?)instance.Output!["fallbackAnswer"]);
        Assert.Null((string?)instance.Output!["answer"]);
        Assert.Equal(NodeState.Skipped, instance.NodeStates["answer"].State);
        Assert.Equal(NodeState.Completed, instance.NodeStates["fallback"].State);
    }

    [Fact]
    public async Task StartAsync_MissingRequiredStartInput_Suspends()
    {
        var harness = await PrepareAsync();
        SetupKnowledgeSearch(harness, hasResult: true);

        var instance = await harness.Engine.StartAsync("doc-qa", new JsonObject());

        Assert.Equal(InstanceStatus.Suspended, instance.Status);
        Assert.Equal(NodeState.Failed, instance.NodeStates["start"].State);
        Assert.Contains("query", instance.NodeStates["start"].ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_NodeFailure_SuspendsThenResumeCompletes()
    {
        var harness = await PrepareAsync();
        SetupKnowledgeSearch(harness, hasResult: true);

        var call = 0;
        harness.PluginInvoker
            .Setup(i => i.InvokeAsync("mock.fallback", It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (call == 0)
                {
                    call++;
                    throw new InvalidOperationException("插件服务暂不可用");
                }

                return new JsonObject { ["answer"] = "兜底成功" };
            });

        // 强制走 false 分支：检索未命中
        harness.PluginInvoker
            .Setup(i => i.InvokeAsync("mock.knowledgeSearch", It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonObject { ["documents"] = new JsonArray(), ["hasResult"] = false });

        var suspended = await harness.Engine.StartAsync("doc-qa", new JsonObject { ["query"] = "问题" });
        Assert.Equal(InstanceStatus.Suspended, suspended.Status);
        Assert.Equal(NodeState.Failed, suspended.NodeStates["fallback"].State);

        var resumed = await harness.Engine.ResumeAsync(suspended.Id);

        Assert.Equal(InstanceStatus.Completed, resumed.Status);
        Assert.Equal("兜底成功", (string?)resumed.Output!["fallbackAnswer"]);
        // 已完成节点（start/search/digest/check）恢复后不重跑
        Assert.Equal(1, resumed.NodeStates["search"].Attempts);
        Assert.Equal(2, resumed.NodeStates["fallback"].Attempts);
    }

    [Fact]
    public async Task StartAsync_UnpublishedDefinition_Throws()
    {
        var harness = new WorkflowTestHarness();
        var definition = WorkflowTestHarness.CreateDocQaDefinition();
        definition.Status = DefinitionStatus.Draft;
        await harness.Store.SaveDefinitionAsync(definition);

        await Assert.ThrowsAsync<WorkflowException>(
            () => harness.Engine.StartAsync("doc-qa", new JsonObject()));
    }
}
