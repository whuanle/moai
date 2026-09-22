using System.Text.Json.Nodes;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
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
/// 未配置图谱或缺少 query 输入时节点失败；topK 夹取 1-50；
/// 输出 hits 扁平（邻居信息在 contents/text 中），contents/text 为 GraphSearchTextHelper 生成的片段（与检索 API 同源同形），text 超长截断.
/// </summary>
public class KnowledgeGraphSearchNodeTests
{
    /// <summary>
    /// 构造一条命中：Text 经 <see cref="GraphSearchTextHelper.BuildHitFragment"/> 生成（与 Client 真实路径同源），避免手写格式漂移.
    /// </summary>
    private static WorkflowGraphSearchHit NewHit(string nodeId, string name, string? typeName, string description, double? score, params GraphNeighbor[] neighbors)
    {
        var hit = new GraphSearchHit(1, nodeId, name, description, 0, typeName, score, neighbors);
        return new WorkflowGraphSearchHit
        {
            KgId = hit.KgId,
            NodeId = hit.NodeId,
            Name = hit.Name,
            EntityTypeName = hit.EntityTypeName,
            Description = hit.Description,
            Score = hit.Score,
            Text = GraphSearchTextHelper.BuildHitFragment(hit, hit.Neighbors),
        };
    }

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
        // 默认返回 2 条命中（第二条得分为空、类型未定义、无邻居，覆盖可空字段与未知类型兜底）
        var harness = new WorkflowTestHarness();
        harness.GraphSearch
            .Setup(c => c.SearchAsync(
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowGraphSearchHit>
            {
                NewHit("n1", "张三", "人物", "MoAI 的作者", 0.91, new GraphNeighbor(null, "out", "李四", "同事")),
                NewHit("n2", "MoAI", null, "开源项目", null),
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

        // contents 与 text 为片段（头部【名称（类型）】描述 + 邻居行），与检索 API 的 Text 段同源同形
        var contents = Assert.IsType<JsonArray>(output["contents"]);
        Assert.Equal(2, contents.Count);
        Assert.Contains("【张三（人物）】MoAI 的作者", (string?)contents[0], StringComparison.Ordinal);
        Assert.Contains("  └─ 关联(out)→ 李四：同事", (string?)contents[0], StringComparison.Ordinal);
        Assert.Contains("【MoAI（未知类型）】开源项目", (string?)contents[1], StringComparison.Ordinal);

        var text = (string?)output["text"];
        Assert.NotNull(text);
        Assert.Contains("【张三（人物）】MoAI 的作者", text, StringComparison.Ordinal);
        Assert.Contains("李四：同事", text, StringComparison.Ordinal);
        Assert.Contains("【MoAI（未知类型）】开源项目", text, StringComparison.Ordinal);
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
    public async Task Text_Truncated_WhenOverLimit()
    {
        var harness = NewHarness();
        harness.GraphSearch
            .Setup(c => c.SearchAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowGraphSearchHit>
            {
                new() { KgId = 1, NodeId = "n1", Name = "大节点", EntityTypeName = "人物", Description = "长描述", Score = 0.5, Text = new string('a', 9000) },
            });
        await harness.Store.SaveDefinitionAsync(CreateDefinition());

        var instance = await harness.Engine.StartAsync("kgs-static", new JsonObject { ["query"] = "q" });

        Assert.Equal(InstanceStatus.Completed, instance.Status);
        var text = (string?)instance.NodeStates["kgs"].Output["text"];
        Assert.NotNull(text);
        Assert.EndsWith("…(已截断)", text, StringComparison.Ordinal);
        Assert.Equal(8192 + "…(已截断)".Length, text.Length);
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
