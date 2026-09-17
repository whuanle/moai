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

    private static WorkflowDefinition CreateScriptConditionDefinition()
    {
        return new WorkflowDefinition
        {
            Id = "script-cond",
            Name = "脚本条件",
            Version = 1,
            Status = DefinitionStatus.Published,
            Nodes =
            [
                new NodeDefinition
                {
                    Key = "start",
                    Name = "开始",
                    Type = NodeTypes.Start,
                    Outputs =
                    [
                        new PortDefinition { Name = "query", FieldType = FieldType.String, IsRequired = true },
                    ],
                },
                new NodeDefinition
                {
                    Key = "check",
                    Name = "脚本条件",
                    Type = NodeTypes.Condition,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        conditionScript = "function condition(inputs, sys, nodes, system) {\n  return (nodes.start.query || '').length > 3;\n}",
                    }),
                },
                new NodeDefinition
                {
                    Key = "end",
                    Name = "结束",
                    Type = NodeTypes.End,
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["result"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "check.result", Required = false },
                    },
                },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "check" },
                new ConnectionDefinition { Id = "c2", Source = "check", Target = "end", Condition = "true" },
                new ConnectionDefinition { Id = "c3", Source = "check", Target = "end", Condition = "false" },
            ],
        };
    }

    [Fact]
    public async Task StartAsync_ConditionScript_RoutesByScriptResult()
    {
        var harness = new WorkflowTestHarness();
        await harness.Store.SaveDefinitionAsync(CreateScriptConditionDefinition());

        // 脚本按 nodes.start.query 长度路由：>3 走真分支，否则走假分支
        var longInstance = await harness.Engine.StartAsync("script-cond", new JsonObject { ["query"] = "hello" });
        Assert.Equal(InstanceStatus.Completed, longInstance.Status);
        var longCheck = longInstance.NodeStates["check"].Output;
        Assert.NotNull(longCheck);
        Assert.True((bool)longCheck["result"]!);

        var shortInstance = await harness.Engine.StartAsync("script-cond", new JsonObject { ["query"] = "hi" });
        Assert.Equal(InstanceStatus.Completed, shortInstance.Status);
        var shortCheck = shortInstance.NodeStates["check"].Output;
        Assert.NotNull(shortCheck);
        Assert.False((bool)shortCheck["result"]!);
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

        // 条件节点透传：输出 = 输入 + result（true 分支：condition 输入为 true）
        var checkOutput = instance.NodeStates["check"].Output;
        Assert.NotNull(checkOutput);
        Assert.True((bool)checkOutput["result"]!);
        Assert.True((bool)checkOutput["condition"]!);

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
    public async Task StartAsync_SystemVariables_DefaultAndOverride()
    {
        // 定义：start → echo(JS 读 system.env) → end；全局变量 env 默认 "test"
        var definition = new WorkflowDefinition
        {
            Id = "sys-vars",
            Name = "全局变量",
            Version = 1,
            Status = DefinitionStatus.Published,
            Variables =
            [
                new GlobalVariableDefinition { Name = "env", FieldType = FieldType.String, DefaultValue = "\"test\"", Description = "环境" },
            ],
            Nodes =
            [
                new NodeDefinition { Key = "start", Name = "开始", Type = NodeTypes.Start },
                new NodeDefinition
                {
                    Key = "echo",
                    Name = "读取",
                    Type = NodeTypes.JavaScript,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        code = "function run(inputs, sys, nodes, system) { return { env: system.env, fromStart: inputs.flag } }",
                    }),
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["flag"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.flag", Required = false },
                    },
                    Outputs = [new PortDefinition { Name = "env", FieldType = FieldType.String }],
                },
                new NodeDefinition { Key = "end", Name = "结束", Type = NodeTypes.End },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "echo" },
                new ConnectionDefinition { Id = "c2", Source = "echo", Target = "end" },
            ],
        };

        var harness = new WorkflowTestHarness();
        await harness.Store.SaveDefinitionAsync(definition);

        // 默认值生效
        var withDefault = await harness.Engine.StartWithDefinitionAsync(definition, new JsonObject { ["flag"] = 1 });
        Assert.Equal(InstanceStatus.Completed, withDefault.Status);
        Assert.Equal("test", (string?)withDefault.NodeStates["echo"].Output!["env"]);

        // 启动传入覆盖默认值
        var withOverride = await harness.Engine.StartWithDefinitionAsync(
            definition,
            new JsonObject { ["flag"] = 2 },
            new JsonObject { ["env"] = "prod" });
        Assert.Equal("prod", (string?)withOverride.NodeStates["echo"].Output!["env"]);

        // 实例持久化全局变量（断点恢复后作用域仍可重建）
        var stored = await harness.Store.FindInstanceByIdAsync(withOverride.Id);
        Assert.NotNull(stored);
        Assert.Equal("prod", (string?)stored.SystemVariables["env"]);
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
