using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class DeleteKnowledgeGraphCommandHandlerTests
{
    private const long KnowledgeGraphId = 7;

    [Fact]
    public async Task Handle_WhenManaged_PurgesGraph()
    {
        using var db = TestSqliteContext.Create();
        var graph = await AddGraphAsync(db, KnowledgeGraphModes.Managed);

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = CreateHandler(db, graph, store.Object);

        await sut.Handle(new DeleteKnowledgeGraphCommand { KnowledgeGraphId = KnowledgeGraphId }, CancellationToken.None);

        store.Verify(x => x.PurgeGraphAsync(KnowledgeGraphId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenConnected_DoesNotPurgeGraph()
    {
        using var db = TestSqliteContext.Create();
        var graph = await AddGraphAsync(db, KnowledgeGraphModes.Connected);

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = CreateHandler(db, graph, store.Object);

        await sut.Handle(new DeleteKnowledgeGraphCommand { KnowledgeGraphId = KnowledgeGraphId }, CancellationToken.None);

        store.Verify(x => x.PurgeGraphAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeleteGraph_CleansVectorCollectionBeforeSoftDelete()
    {
        using var db = TestSqliteContext.Create();
        var graph = await AddGraphAsync(db, KnowledgeGraphModes.Managed);

        // 回调时记录实体软删状态：清理必须发生在 SaveChanges（软删）之前，
        // 否则全局 IsDeleted 过滤器会让向量存储的维度解析查不到图谱，清理变成空操作
        long? isDeletedAtCleanup = null;
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.DeleteGraphVectorsAsync(KnowledgeGraphId, It.IsAny<CancellationToken>()))
            .Callback<long, CancellationToken>((_, _) => isDeletedAtCleanup = graph.IsDeleted)
            .Returns(Task.CompletedTask);

        var sut = CreateHandler(db, graph, new Mock<IKnowledgeGraphStore>().Object, vectorStore.Object);

        await sut.Handle(new DeleteKnowledgeGraphCommand { KnowledgeGraphId = KnowledgeGraphId }, CancellationToken.None);

        vectorStore.Verify(x => x.DeleteGraphVectorsAsync(KnowledgeGraphId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(0, isDeletedAtCleanup);
        Assert.Equal(0, await db.Context.KnowledgeGraphs.CountAsync(x => x.Id == KnowledgeGraphId, CancellationToken.None));
        Assert.Equal(1, await db.Context.KnowledgeGraphs.IgnoreQueryFilters().CountAsync(x => x.Id == KnowledgeGraphId, CancellationToken.None));
    }

    private static async Task<KnowledgeGraphEntity> AddGraphAsync(TestSqliteContext db, string mode)
    {
        var graph = new KnowledgeGraphEntity
        {
            Id = KnowledgeGraphId,
            TeamId = 1,
            Name = "图谱",
            Description = string.Empty,
            Mode = mode,
            AvatarPath = string.Empty,
        };
        db.Context.KnowledgeGraphs.Add(graph);
        await db.Context.SaveChangesAsync(CancellationToken.None);
        return graph;
    }

    private static DeleteKnowledgeGraphCommandHandler CreateHandler(TestSqliteContext db, KnowledgeGraphEntity graph, IKnowledgeGraphStore store, IKgEmbeddingVectorStore? vectorStore = null)
    {
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(KnowledgeGraphId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((graph, MoAI.Database.Enums.TeamRole.Admin));
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        return new DeleteKnowledgeGraphCommandHandler(db.Context, authorizer.Object, store, settings.Object, new Mock<IKnowledgeGraphIntrospectionCache>().Object, vectorStore ?? new Mock<IKgEmbeddingVectorStore>().Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteKnowledgeGraphCommandHandler>.Instance);
    }
}
