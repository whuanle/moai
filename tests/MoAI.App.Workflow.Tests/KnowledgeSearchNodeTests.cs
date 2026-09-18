using System.Text.Json.Nodes;
using Moq;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Nodes.Builtin;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// 知识库检索节点（knowledgeSearch）：一个节点只绑定一个知识库——
/// 输入 wikiId（变量绑定）优先于 config.wikiId（静态选择），兼容旧版复数 wikiIds；
/// 未配置知识库时节点失败；空变量绑定不阻塞保存.
/// </summary>
public class KnowledgeSearchNodeTests
{
    /// <summary>
    /// start(query, wikiId) → ks(知识库检索) → end；单数/旧版复数由 legacy 参数控制.
    /// </summary>
    private static WorkflowDefinition CreateDefinition(bool withStaticWiki, bool withWikiInput, bool legacy = false)
    {
        var ksInputs = new Dictionary<string, FieldBinding>
        {
            ["query"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.query", Required = true },
        };
        if (withWikiInput)
        {
            ksInputs[legacy ? "wikiIds" : "wikiId"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.wikiId", Required = false };
        }

        object ksConfigValue = !withStaticWiki
            ? new { topK = 3 }
            : legacy
                ? new { wikiIds = new[] { 1L }, topK = 3 }
                : new { wikiId = 1, topK = 3 };

        return new WorkflowDefinition
        {
            Id = "ks-dynamic",
            Name = "知识库动态检索",
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
                        new PortDefinition { Name = "wikiId", FieldType = FieldType.Dynamic },
                    ],
                },
                new NodeDefinition
                {
                    Key = "ks",
                    Name = "知识库检索",
                    Type = NodeTypes.KnowledgeSearch,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(ksConfigValue),
                    Inputs = ksInputs,
                    Outputs =
                    [
                        new PortDefinition { Name = "query", FieldType = FieldType.String },
                        new PortDefinition { Name = "count", FieldType = FieldType.Number },
                        new PortDefinition { Name = "hits", FieldType = FieldType.Array },
                        new PortDefinition { Name = "contents", FieldType = FieldType.Array },
                        new PortDefinition { Name = "text", FieldType = FieldType.String },
                    ],
                },
                new NodeDefinition
                {
                    Key = "end",
                    Name = "结束",
                    Type = NodeTypes.End,
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["count"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "ks.count", Required = false },
                    },
                },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "k1", Source = "start", Target = "ks" },
                new ConnectionDefinition { Id = "k2", Source = "ks", Target = "end" },
            ],
        };
    }

    private static WorkflowTestHarness NewHarness()
    {
        // 默认返回 1 条命中（文档「设计文档」）
        var harness = new WorkflowTestHarness();
        harness.WikiSearch
            .Setup(c => c.SearchAsync(
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowWikiSearchHit>
            {
                new() { WikiId = 2, DocumentId = 9, DocumentName = "设计文档", ChunkId = 33, Content = "工作流引擎设计", Score = 0.87 },
            });
        return harness;
    }

    [Fact]
    public async Task InputWikiId_OverridesStaticConfig()
    {
        var harness = NewHarness();
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: true, withWikiInput: true));

        var instance = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "q", ["wikiId"] = 2 });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        // 输入绑定的 2 优先于静态配置的 1
        harness.WikiSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 2 })),
                "q",
                3,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MissingInput_FallsBackToStaticConfig()
    {
        var harness = NewHarness();
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: true, withWikiInput: true));

        var instance = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        harness.WikiSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1 })),
                "q",
                3,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InputValue_Formats_NumberString_And_LegacyWikiIds()
    {
        var harness = NewHarness();

        // 单数字符串 "3" → [3]
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: false, withWikiInput: true));
        var instance = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "q", ["wikiId"] = "3" });
        Assert.Equal(InstanceStatus.Completed, instance.Status);
        harness.WikiSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 3 })),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 旧版复数：输入 wikiIds 数组 [5, "6"] → [5, 6]
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: false, withWikiInput: true, legacy: true));
        var instance2 = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "q", ["wikiId"] = new JsonArray { 5, "6" } });
        Assert.Equal(InstanceStatus.Completed, instance2.Status);
        harness.WikiSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 5, 6 })),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 旧版复数：config.wikiIds 静态兜底
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: true, withWikiInput: true, legacy: true));
        var instance3 = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "q" });
        Assert.Equal(InstanceStatus.Completed, instance3.Status);
        harness.WikiSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1 })),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoWikiConfigured_NodeFails()
    {
        var harness = NewHarness();
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: false, withWikiInput: true));

        var instance = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Suspended, instance.Status);
        var ks = instance.NodeStates["ks"];
        Assert.Equal(NodeState.Failed, ks.State);
        Assert.Contains("wikiId", ks.ErrorMessage);
    }

    [Fact]
    public async Task Output_Contains_StructuredResult()
    {
        var harness = NewHarness();
        await harness.Store.SaveDefinitionAsync(CreateDefinition(withStaticWiki: true, withWikiInput: false));

        var instance = await harness.Engine.StartAsync("ks-dynamic", new JsonObject { ["query"] = "什么是工作流？" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var output = instance.NodeStates["ks"].Output;
        Assert.NotNull(output);
        Assert.Equal("什么是工作流？", (string?)output["query"]);
        Assert.Equal(1, (int?)output["count"]);
        var hits = Assert.IsType<JsonArray>(output["hits"]);
        Assert.Single(hits);
        var hit = Assert.IsType<JsonObject>(hits[0]);
        Assert.Equal("设计文档", (string?)hit["documentName"]);
        Assert.Equal(0.87, (double?)hit["score"]);
        var contents = Assert.IsType<JsonArray>(output["contents"]);
        Assert.Single(contents);
        Assert.Contains("【设计文档】", (string?)output["text"]);
    }

    [Fact]
    public async Task Executor_Direct_InputNumber_IsUsed()
    {
        var harness = NewHarness();
        var node = CreateDefinition(withStaticWiki: true, withWikiInput: true).Nodes[1];
        var inputs = new JsonObject { ["query"] = "q", ["wikiId"] = 2 };
        var scope = new WorkflowVariableScope();
        var executor = new KnowledgeSearchNodeExecutor(harness.WikiSearch.Object);
        var result = await executor.ExecuteAsync(new NodeExecutionContext("i1", node, inputs, scope, new WorkflowEventPublisher()), CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        harness.WikiSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 2 })),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Validator_EmptyVariableBinding_IsAllowed()
    {
        var validator = new WorkflowValidator();
        var definition = CreateDefinition(withStaticWiki: true, withWikiInput: true);
        definition.Nodes[1].Inputs["wikiId"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = string.Empty, Required = false };

        Assert.Empty(validator.GetErrors(definition));

        // 非空但格式非法仍报错
        definition.Nodes[1].Inputs["wikiId"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "3", Required = false };
        Assert.Contains(validator.GetErrors(definition), e => e.Contains("不存在"));
    }
}
