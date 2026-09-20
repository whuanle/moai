using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Nodes.Builtin;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// 问题分类节点（questionClassifier）：AI 模型把用户问题归入预定义分类之一，
/// 输出 = 输入透传 + result（分类 id）/ className（分类名），调度器按 result 匹配出边分类标记.
/// 序号数字 → 分类名匹配 → 兜底第一个分类；缺模型/分类/用户问题时节点失败.
/// </summary>
public class QuestionClassifierNodeTests
{
    /// <summary>
    /// start(query, history) → clf(问题分类) → [c1] reply1 / [c2] reply2 → end.
    /// </summary>
    private static WorkflowDefinition CreateDefinition(Dictionary<string, object?> config, bool withQuery = true, bool withHistory = false)
    {
        var inputs = new Dictionary<string, FieldBinding>();
        if (withQuery)
        {
            inputs["query"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.query" };
        }

        if (withHistory)
        {
            inputs["history"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.history", Required = false };
        }

        return new WorkflowDefinition
        {
            Id = "clf-test",
            Name = "问题分类",
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
                        new PortDefinition { Name = "history", FieldType = FieldType.Array },
                    ],
                },
                new NodeDefinition
                {
                    Key = "clf",
                    Name = "问题分类",
                    Type = NodeTypes.QuestionClassifier,
                    Config = JsonSerializer.SerializeToElement(config),
                    Inputs = inputs,
                    Outputs =
                    [
                        new PortDefinition { Name = "result", FieldType = FieldType.String },
                        new PortDefinition { Name = "className", FieldType = FieldType.String },
                    ],
                },
                new NodeDefinition
                {
                    Key = "reply1",
                    Name = "分支一",
                    Type = NodeTypes.JavaScript,
                    Config = JsonSerializer.SerializeToElement(new { code = "function run(inputs) { return { answer: 'one' } }" }),
                },
                new NodeDefinition
                {
                    Key = "reply2",
                    Name = "分支二",
                    Type = NodeTypes.JavaScript,
                    Config = JsonSerializer.SerializeToElement(new { code = "function run(inputs) { return { answer: 'two' } }" }),
                },
                new NodeDefinition
                {
                    Key = "end",
                    Name = "结束",
                    Type = NodeTypes.End,
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["one"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "reply1.answer", Required = false },
                        ["two"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "reply2.answer", Required = false },
                    },
                },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "clf" },
                new ConnectionDefinition { Id = "c2", Source = "clf", Target = "reply1", Condition = "c1" },
                new ConnectionDefinition { Id = "c3", Source = "clf", Target = "reply2", Condition = "c2" },
                new ConnectionDefinition { Id = "c4", Source = "reply1", Target = "end" },
                new ConnectionDefinition { Id = "c5", Source = "reply2", Target = "end" },
            ],
        };
    }

    private static Dictionary<string, object?> ClassifierConfig(
        string? model = "mock-model",
        object? classes = null,
        int? historyCount = null,
        string? background = null)
    {
        var config = new Dictionary<string, object?>();
        if (model != null)
        {
            config["aiModelId"] = model;
        }

        if (background != null)
        {
            config["backgroundKnowledge"] = background;
        }

        if (historyCount != null)
        {
            config["historyCount"] = historyCount;
        }

        config["classes"] = classes ?? new List<object>
        {
            new { id = "c1", label = "售前咨询" },
            new { id = "c2", label = "售后咨询" },
        };
        return config;
    }

    /// <summary>
    /// Mock AI 客户端固定返回 <paramref name="answer"/>，返回的读取函数在执行后取回捕获的系统提示词/历史/模型参数.
    /// </summary>
    private static (WorkflowTestHarness Harness, Func<(string? System, JsonArray? History, string? Model)> Capture) SetupAiChat(string answer)
    {
        string? system = null;
        JsonArray? history = null;
        string? model = null;
        var harness = new WorkflowTestHarness();
        harness.AiChat
            .Setup(c => c.CompleteAsync(
                It.IsAny<AiChatRequest>(),
                It.IsAny<Func<string, Task>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer)
            .Callback<AiChatRequest, Func<string, Task>?, CancellationToken>((r, _o, _ct) =>
            {
                system = r.SystemPrompt;
                history = r.History;
                model = r.Model;
            });
        return (harness, () => (system, history, model));
    }

    [Fact]
    public async Task Execute_NumberAnswer_RoutesSecondBranch()
    {
        var (harness, capture) = SetupAiChat("2");
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig()));

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "怎么退款" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        Assert.Equal("mock-model", capture().Model);
        var clf = instance.NodeStates["clf"];
        Assert.Equal(NodeState.Completed, clf.State);
        Assert.Equal("c2", clf.Output?["result"]?.GetValue<string>());
        Assert.Equal("售后咨询", clf.Output?["className"]?.GetValue<string>());
        // 命中分支执行、另一分支跳过
        Assert.Equal(NodeState.Skipped, instance.NodeStates["reply1"].State);
        Assert.Equal(NodeState.Completed, instance.NodeStates["reply2"].State);
        Assert.Equal("two", instance.Output?["two"]?.GetValue<string>());
        Assert.Null(instance.Output?["one"]?.GetValue<string>());
    }

    [Fact]
    public async Task Execute_LabelAnswer_MatchesByLabel()
    {
        var (harness, _) = SetupAiChat("用户的问题是售前咨询相关");
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig()));

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "多少钱" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        Assert.Equal("c1", instance.NodeStates["clf"].Output?["result"]?.GetValue<string>());
        Assert.Equal("售前咨询", instance.NodeStates["clf"].Output?["className"]?.GetValue<string>());
    }

    [Fact]
    public async Task Execute_UnparseableAnswer_FallsBackToFirstClass()
    {
        var (harness, _) = SetupAiChat("抱歉我无法判断");
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig()));

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "随便说说" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var clf = instance.NodeStates["clf"];
        Assert.Equal("c1", clf.Output?["result"]?.GetValue<string>());
        Assert.Equal("售前咨询", clf.Output?["className"]?.GetValue<string>());
    }

    [Fact]
    public async Task Execute_PromptContainsClassesAndBackground()
    {
        var (harness, capture) = SetupAiChat("1");
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig(background: "商城仅支持7天退货")));

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "能退货吗" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var system = capture().System;
        Assert.NotNull(system);
        Assert.Contains("类型列表", system);
        Assert.Contains("1. 售前咨询", system);
        Assert.Contains("2. 售后咨询", system);
        Assert.Contains("背景知识：商城仅支持7天退货", system);
        Assert.Contains("只输出类型序号数字", system);
    }

    [Fact]
    public async Task Execute_HistoryTruncatedToConfiguredCount()
    {
        var (harness, capture) = SetupAiChat("2");
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig(historyCount: 2), withHistory: true));

        var historyInput = new JsonArray();
        for (var i = 0; i < 5; i++)
        {
            historyInput.Add(new JsonObject { ["role"] = "user", ["content"] = $"msg-{i}" });
        }

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "最新问题", ["history"] = historyInput });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var history = capture().History;
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal("msg-3", history[0]?["content"]?.GetValue<string>());
        Assert.Equal("msg-4", history[1]?["content"]?.GetValue<string>());
    }

    [Fact]
    public async Task Execute_MissingModel_NodeFails()
    {
        var (harness, _) = SetupAiChat("1");
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig(model: null)));

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Suspended, instance.Status);
        var clf = instance.NodeStates["clf"];
        Assert.Equal(NodeState.Failed, clf.State);
        Assert.Contains("aiModelId", clf.ErrorMessage);
    }

    /// <summary>
    /// 直接构造执行上下文调用执行器（缺 classes 的定义过不了引擎校验，缺 query 会先被 start 必需参数拦下）.
    /// </summary>
    private static async Task<NodeExecutionResult> ExecuteDirectAsync(
        WorkflowTestHarness harness,
        Dictionary<string, object?> config,
        JsonObject inputs)
    {
        var node = new NodeDefinition
        {
            Key = "clf",
            Name = "问题分类",
            Type = NodeTypes.QuestionClassifier,
            Config = JsonSerializer.SerializeToElement(config),
        };
        var context = new NodeExecutionContext(
            "inst-1",
            node,
            inputs,
            new Mock<IWorkflowVariableScope>().Object,
            new Mock<Events.IWorkflowEventPublisher>().Object);
        var executor = new QuestionClassifierNodeExecutor(harness.AiChat.Object);
        return await executor.ExecuteAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteDirect_MissingClasses_FailsWithoutCallingModel()
    {
        var (harness, _) = SetupAiChat("1");

        var result = await ExecuteDirectAsync(harness, ClassifierConfig(classes: new List<object>()), new JsonObject { ["query"] = "q" });

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("classes", result.ErrorMessage);
        harness.AiChat.Verify(c => c.CompleteAsync(
            It.IsAny<AiChatRequest>(),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDirect_MissingQuery_FailsWithoutCallingModel()
    {
        var (harness, _) = SetupAiChat("1");

        var result = await ExecuteDirectAsync(harness, ClassifierConfig(), new JsonObject());

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("query", result.ErrorMessage);
        harness.AiChat.Verify(c => c.CompleteAsync(
            It.IsAny<AiChatRequest>(),
            It.IsAny<Func<string, Task>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDirect_EmptyQuery_Fails()
    {
        var (harness, _) = SetupAiChat("1");

        var result = await ExecuteDirectAsync(harness, ClassifierConfig(), new JsonObject { ["query"] = "  " });

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("query", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_AiChatThrows_NodeFailsWithReason()
    {
        var harness = new WorkflowTestHarness();
        harness.AiChat
            .Setup(c => c.CompleteAsync(
                It.IsAny<AiChatRequest>(),
                It.IsAny<Func<string, Task>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("渠道不可用"));
        await harness.Store.SaveDefinitionAsync(CreateDefinition(ClassifierConfig()));

        var instance = await harness.Engine.StartAsync("clf-test", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Suspended, instance.Status);
        var clf = instance.NodeStates["clf"];
        Assert.Equal(NodeState.Failed, clf.State);
        Assert.Contains("问题分类执行失败", clf.ErrorMessage);
        Assert.Contains("渠道不可用", clf.ErrorMessage);
    }

    [Fact]
    public void Validate_ClassifierWithoutClasses_ReportsError()
    {
        var definition = CreateDefinition(ClassifierConfig(classes: new List<object>()));
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("至少需要配置一个分类"));
    }

    [Fact]
    public void Validate_ClassifierEmptyLabel_ReportsError()
    {
        var definition = CreateDefinition(ClassifierConfig(classes: new List<object>
        {
            new { id = "c1", label = string.Empty },
            new { id = "c2", label = "售后咨询" },
        }));
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("分类值不可为空"));
    }

    [Fact]
    public void Validate_ClassifierDuplicateLabels_ReportsError()
    {
        var definition = CreateDefinition(ClassifierConfig(classes: new List<object>
        {
            new { id = "c1", label = "咨询" },
            new { id = "c2", label = "咨询" },
        }));
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("重复的分类名称"));
    }

    [Fact]
    public void Validate_ClassifierInvalidEdgeMarker_ReportsError()
    {
        var definition = CreateDefinition(ClassifierConfig());
        definition.Connections.RemoveAll(c => c.Id == "c3");
        definition.Connections.Add(new ConnectionDefinition { Id = "c9", Source = "clf", Target = "reply2", Condition = "else" });
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Contains(errors, e => e.Contains("分类标记无效"));
    }

    [Fact]
    public void Validate_ValidClassifierDefinition_HasNoErrors()
    {
        var definition = CreateDefinition(ClassifierConfig());
        var validator = new WorkflowValidator();

        var errors = validator.GetErrors(definition);

        Assert.Empty(errors);
    }
}
