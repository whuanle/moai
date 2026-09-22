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
/// 知识图谱检索节点（kgSearch）：v1 仅支持 config.graphId 静态选图（无 graphId 输入变量绑定），
/// 未配置图谱或缺少 query 输入时节点失败；topK 夹取 1-50；输出 hits 扁平（邻居信息在 contents/text 中）.
/// </summary>
public class KnowledgeGraphSearchNodeTests
{
    /// <summary>
    /// start(query) → kgs(知识图谱检索) → end.
    /// </summary>
    private static WorkflowDefinition CreateDefinition(bool withGraphId = true, bool withTopK = true)
    {
        var configValue = (withGraphId, withTopK) switch
        {
            (true, true) => new { graphId = 1, topK = 3 },
            (true, false) => (object)new { graphId = 1 },
            _ => (object)new { topK = 3 },
        };

        return new WorkflowDefinition
        {
            Id = "kgs-static",
            Name = "知识图谱静态检索",
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
                    Key = "kgs",
                    Name = "知识图谱检索",
                    Type = NodeTypes.KnowledgeGraphSearch,
                    Config = System.Text.Json.JsonSerializer.SerializeToElement(configValue),
                    Inputs = new Dictionary<string, FieldBinding>
                    {
                        ["query"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "start.query", Required = true },
                    },
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
                        ["count"] = new FieldBinding { ExpressionType = ExpressionType.Variable, Value = "kgs.count", Required = false },
                    },
                },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "g1", Source = "start", Target = "kgs" },
                new ConnectionDefinition { Id = "g2", Source = "kgs", Target = "end" },
            ],
        };
    }

    private static WorkflowTestHarness NewHarness()
    {
        // 默认返回 2 条命中（第二条得分为空、类型未定义，覆盖可空字段序列化）
        var harness = new WorkflowTestHarness();
        harness.GraphSearch
            .Setup(c => c.SearchAsync(
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowGraphSearchHit>
            {
                new() { KgId = 1, NodeId = "n1", Name = "张三", EntityTypeName = "人物", Description = "MoAI 的作者", Score = 0.91, Text = "张三：MoAI 的作者\n  └─ 关联(out)→ 李四：同事" },
                new() { KgId = 1, NodeId = "n2", Name = "MoAI", EntityTypeName = null, Description = "开源项目", Score = null, Text = "MoAI：开源项目" },
            });
        return harness;
    }

    [Fact]
    public async Task StaticConfig_UsesConfigGraphId()
    {
        var harness = NewHarness();
        await harness.Store.SaveDefinitionAsync(CreateDefinition());

        var instance = await harness.Engine.StartAsync("kgs-static", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        harness.GraphSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1 })),
                "q",
                3,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Output_Contains_StructuredResult()
    {
        var harness = NewHarness();
        await harness.Store.SaveDefinitionAsync(CreateDefinition());

        var instance = await harness.Engine.StartAsync("kgs-static", new JsonObject { ["query"] = "谁写了 MoAI？" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var output = instance.NodeStates["kgs"].Output;
        Assert.NotNull(output);
        Assert.Equal("谁写了 MoAI？", (string?)output["query"]);
        Assert.Equal(2, (int?)output["count"]);

        var hits = Assert.IsType<JsonArray>(output["hits"]);
        Assert.Equal(2, hits.Count);

        var first = Assert.IsType<JsonObject>(hits[0]);
        Assert.Equal(1L, (long?)first["kgId"]);
        Assert.Equal("n1", (string?)first["nodeId"]);
        Assert.Equal("张三", (string?)first["name"]);
        Assert.Equal("人物", (string?)first["entityType"]);
        Assert.Equal("MoAI 的作者", (string?)first["description"]);
        Assert.Equal(0.91, (double?)first["score"]);

        // 可空字段：score 缺失、类型未定义 → JSON null
        var second = Assert.IsType<JsonObject>(hits[1]);
        Assert.Null((double?)second["score"]);
        Assert.Null((string?)second["entityType"]);

        var contents = Assert.IsType<JsonArray>(output["contents"]);
        Assert.Equal(2, contents.Count);
        Assert.Contains("└─", (string?)contents[0]);

        var text = (string?)output["text"];
        Assert.NotNull(text);
        Assert.Contains("张三", text);
        Assert.Contains("李四", text);
        Assert.Contains("MoAI：开源项目", text);
    }

    [Fact]
    public async Task EmptyHits_CompletesWithZeroCount()
    {
        var harness = NewHarness();
        harness.GraphSearch
            .Setup(c => c.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await harness.Store.SaveDefinitionAsync(CreateDefinition());

        var instance = await harness.Engine.StartAsync("kgs-static", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var output = instance.NodeStates["kgs"].Output;
        Assert.Equal(0, (int?)output["count"]);
        Assert.Equal(string.Empty, (string?)output["text"]);
    }

    [Fact]
    public async Task Executor_Direct_MissingQuery_Fails()
    {
        var harness = NewHarness();
        var node = CreateDefinition().Nodes[1];
        var executor = new KnowledgeGraphSearchNodeExecutor(harness.GraphSearch.Object);
        var result = await executor.ExecuteAsync(
            new NodeExecutionContext("i1", node, new JsonObject(), new WorkflowVariableScope(), new WorkflowEventPublisher()),
            CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("query", result.ErrorMessage);
    }

    [Fact]
    public async Task Executor_Direct_MissingGraphId_Fails()
    {
        var harness = NewHarness();
        var node = CreateDefinition(withGraphId: false).Nodes[1];
        var executor = new KnowledgeGraphSearchNodeExecutor(harness.GraphSearch.Object);
        var result = await executor.ExecuteAsync(
            new NodeExecutionContext("i1", node, new JsonObject { ["query"] = "q" }, new WorkflowVariableScope(), new WorkflowEventPublisher()),
            CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("graphId", result.ErrorMessage);
    }

    [Fact]
    public async Task Executor_Direct_TopK_IsClamped()
    {
        var harness = NewHarness();

        // 超上限夹取到 50
        var node = CreateDefinition();
        node.Nodes[1].Config = System.Text.Json.JsonSerializer.SerializeToElement(new { graphId = 2, topK = 999 });
        var executor = new KnowledgeGraphSearchNodeExecutor(harness.GraphSearch.Object);
        await executor.ExecuteAsync(
            new NodeExecutionContext("i1", node.Nodes[1], new JsonObject { ["query"] = "q" }, new WorkflowVariableScope(), new WorkflowEventPublisher()),
            CancellationToken.None);
        harness.GraphSearch.Verify(
            c => c.SearchAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 2 })),
                It.IsAny<string>(),
                50,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 缺省 topK → 5
        var node2 = CreateDefinition(withTopK: false);
        await executor.ExecuteAsync(
            new NodeExecutionContext("i2", node2.Nodes[1], new JsonObject { ["query"] = "q" }, new WorkflowVariableScope(), new WorkflowEventPublisher()),
            CancellationToken.None);
        harness.GraphSearch.Verify(
            c => c.SearchAsync(
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<string>(),
                5,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
