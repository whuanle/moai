using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Consumers.Events;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Database.Enums;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class MergeKnowledgeGraphNodesCommandHandlerTests
{
    private const long KnowledgeGraphId = 1;
    private const long TypeA = 10;
    private const long RtFree = 20;
    private const long RtConstrained = 30;

    [Fact]
    public async Task Handle_MergesSources_RehangsEdgesAndPublishesDeltas()
    {
        using var db = TestSqliteContext.Create();
        await AddGraphAsync(db);
        await AddRelationTypeAsync(db, RtFree, sourceTypeId: null);

        var store = BuildStore();
        var publisher = new Mock<IMessagePublisher>();
        var sut = CreateHandler(db, store.Object, publisher.Object);

        var response = await sut.Handle(new MergeKnowledgeGraphNodesCommand
        {
            KnowledgeGraphId = KnowledgeGraphId,
            TargetNodeId = "T",
            SourceNodeIds = ["S1", "S2"],
        }, CancellationToken.None);

        // 重挂计算：A→S1 变 A→T（目标已有同三元组跳过）；S2→B 变 T→B（新建）；S1→S2 自环丢弃
        store.Verify(x => x.MergeNodesAsync(
            KnowledgeGraphId,
            "T",
            It.Is<string>(desc => desc.Contains("目标描述") && desc.Contains("源1描述")),
            It.IsAny<string?>(),
            It.Is<IReadOnlyList<KnowledgeGraphEdgeInput>>(list => list.Count == 1 && list[0].SourceNodeId == "T" && list[0].TargetNodeId == "B" && list[0].RelationTypeId == RtFree),
            It.Is<IReadOnlyList<string>>(list => list.SequenceEqual(new[] { "S1", "S2" })),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, response.RehungEdges);
        Assert.Equal(1, response.DroppedSelfLoopEdges);
        Assert.Equal(2, response.RemovedNodes);

        // 向量增量：目标 upsert + 源批量删除
        publisher.Verify(x => x.AutoPublishAsync(It.Is<KgNodeEmbeddingDeltaMessage>(m => m.KgId == KnowledgeGraphId && m.UpsertNodeIds!.SequenceEqual(new[] { "T" })), null, It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.AutoPublishAsync(It.Is<KgNodeEmbeddingDeltaMessage>(m => m.KgId == KnowledgeGraphId && m.DeleteNodeIds!.SequenceEqual(new[] { "S1", "S2" })), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRelationConstraintViolated_Throws400()
    {
        using var db = TestSqliteContext.Create();
        await AddGraphAsync(db);
        // 关系类型要求起点类型 99，目标节点类型为 10 → 重挂后不满足
        await AddRelationTypeAsync(db, RtConstrained, sourceTypeId: 99);

        var store = BuildStore(constrained: true);
        var sut = CreateHandler(db, store.Object, new Mock<IMessagePublisher>().Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new MergeKnowledgeGraphNodesCommand
        {
            KnowledgeGraphId = KnowledgeGraphId,
            TargetNodeId = "T",
            SourceNodeIds = ["S1"],
        }, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        store.Verify(x => x.MergeNodesAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<KnowledgeGraphEdgeInput>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTouchingEdgesReachThresholdWithoutConfirm_Throws409()
    {
        using var db = TestSqliteContext.Create();
        await AddGraphAsync(db);
        await AddRelationTypeAsync(db, RtFree, sourceTypeId: null);

        var store = BuildStore(touchingEdgeCount: 20);
        var sut = CreateHandler(db, store.Object, new Mock<IMessagePublisher>().Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new MergeKnowledgeGraphNodesCommand
        {
            KnowledgeGraphId = KnowledgeGraphId,
            TargetNodeId = "T",
            SourceNodeIds = ["S1"],
        }, CancellationToken.None));
        Assert.Equal(409, ex.StatusCode);

        // 携带确认标记后放行
        await sut.Handle(new MergeKnowledgeGraphNodesCommand
        {
            KnowledgeGraphId = KnowledgeGraphId,
            TargetNodeId = "T",
            SourceNodeIds = ["S1"],
            Confirm = true,
        }, CancellationToken.None);
        store.Verify(x => x.MergeNodesAsync(It.IsAny<long>(), "T", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<KnowledgeGraphEdgeInput>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSourceMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        await AddGraphAsync(db);

        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "T", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("T", KnowledgeGraphId, TypeA, "目标", "目标描述"));
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "S1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeGraphNodeRecord?)null);

        var sut = CreateHandler(db, store.Object, new Mock<IMessagePublisher>().Object);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new MergeKnowledgeGraphNodesCommand
        {
            KnowledgeGraphId = KnowledgeGraphId,
            TargetNodeId = "T",
            SourceNodeIds = ["S1"],
        }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }

    /// <summary>构造图存储桩：目标 T（类型 10）、源 S1/S2（类型 10）、触边 A→S1 / S2→B / S1→S2（自环）；目标已有 A→T 同三元组.</summary>
    private static Mock<IKnowledgeGraphStore> BuildStore(bool constrained = false, int touchingEdgeCount = 3)
    {
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "T", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("T", KnowledgeGraphId, TypeA, "目标", "目标描述"));
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "S1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("S1", KnowledgeGraphId, TypeA, "源1", "源1描述"));
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "S2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("S2", KnowledgeGraphId, TypeA, "源2", "源2描述"));

        var touching = new List<KnowledgeGraphEdgeRecord>
        {
            // constrained=true 场景换成带约束关系类型的出边（S1 为起点 → 重挂后目标作起点，类型须满足 99）
            constrained
                ? new KnowledgeGraphEdgeRecord("E1", KnowledgeGraphId, RtConstrained, "S1", "A")
                : new KnowledgeGraphEdgeRecord("E1", KnowledgeGraphId, RtFree, "A", "S1"),
        };
        if (!constrained)
        {
            touching.Add(new KnowledgeGraphEdgeRecord("E2", KnowledgeGraphId, RtFree, "S2", "B"));
            touching.Add(new KnowledgeGraphEdgeRecord("E3", KnowledgeGraphId, RtFree, "S1", "S2"));
            // 补足到指定触边数（A→Sx 平行边形态）
            while (touching.Count < touchingEdgeCount)
            {
                touching.Add(new KnowledgeGraphEdgeRecord($"E{touching.Count}", KnowledgeGraphId, RtFree, "S1", $"X{touching.Count}"));
            }
        }
        store.Setup(x => x.GetEdgesTouchingNodesAsync(KnowledgeGraphId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(touching);

        store.Setup(x => x.GetExistingEdgeKeysAsync(KnowledgeGraphId, It.IsAny<IReadOnlyList<(string SourceNodeId, long RelationTypeId, string TargetNodeId)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { $"A|{RtFree}|T" });

        store.Setup(x => x.MergeNodesAsync(KnowledgeGraphId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<KnowledgeGraphEdgeInput>>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, string _, string _, string? _, IReadOnlyList<KnowledgeGraphEdgeInput> edges, IReadOnlyList<string> _, CancellationToken _) => edges.Count);
        return store;
    }

    private static MergeKnowledgeGraphNodesCommandHandler CreateHandler(TestSqliteContext db, IKnowledgeGraphStore store, IMessagePublisher publisher)
    {
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer
            .Setup(x => x.AuthorizeManagedAsync(KnowledgeGraphId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity(), TeamRole.Admin));
        return new MergeKnowledgeGraphNodesCommandHandler(db.Context, authorizer.Object, store, publisher, NullLogger<MergeKnowledgeGraphNodesCommandHandler>.Instance);
    }

    private static async Task AddGraphAsync(TestSqliteContext db)
    {
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity
        {
            Id = KnowledgeGraphId,
            TeamId = 1,
            Name = "图谱",
            Description = string.Empty,
            Mode = KnowledgeGraphModes.Managed,
            AvatarPath = string.Empty,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task AddRelationTypeAsync(TestSqliteContext db, long id, long? sourceTypeId)
    {
        db.Context.KnowledgeGraphRelationTypes.Add(new KnowledgeGraphRelationTypeEntity
        {
            Id = id,
            KnowledgeGraphId = KnowledgeGraphId,
            Name = $"关系{id}",
            Color = "#1677ff",
            Description = string.Empty,
            SourceTypeId = sourceTypeId,
            TargetTypeId = null,
            Sort = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
    }
}
