using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.AIChannel.Services;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Consumers.Events;
using MoAI.KnowledgeGraph.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KgEmbeddingServiceTests
{
    private const long KnowledgeGraphId = 11;

    private static readonly Guid EmbeddingModelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChannelId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ProcessDeltaAsync_WhenModelNotConfigured_ReturnsWithoutTouchingVectorStore()
    {
        using var db = await CreateDbAsync(withModel: false);
        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var provider = new Mock<IEmbeddingGeneratorProvider>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, vectorStore.Object, provider.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["n1"] }, CancellationToken.None);

        vectorStore.VerifyNoOtherCalls();
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessDeltaAsync_WhenGraphMissing_ReturnsSilently()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, vectorStore.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = 999, UpsertNodeIds = ["n1"] }, CancellationToken.None);

        vectorStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessDeltaAsync_WhenConnectedGraph_ReturnsSilently()
    {
        using var db = await CreateDbAsync(mode: "connected");
        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, vectorStore.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["n1"] }, CancellationToken.None);

        vectorStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessDeltaAsync_WhenModelUnauthorized_ReturnsSilently()
    {
        using var db = await CreateDbAsync(isPublic: false);
        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var provider = new Mock<IEmbeddingGeneratorProvider>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, vectorStore.Object, provider.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["n1"] }, CancellationToken.None);

        vectorStore.VerifyNoOtherCalls();
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessDeltaAsync_UpsertNode_ReplacesWithExpectedRecord()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNodeAsync(KnowledgeGraphId, "n1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("n1", KnowledgeGraphId, 5, "名称", "描述"));

        var generator = CreateGenerator();
        var sut = CreateSut(db.Context, vectorStore.Object, provider: CreateGeneratorProvider(generator.Object).Object, graphStore: graphStore.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["n1"] }, CancellationToken.None);

        generator.Verify(x => x.GenerateAsync(
            It.Is<IEnumerable<string>>(inputs => inputs.Single() == "名称\n描述"),
            It.Is<EmbeddingGenerationOptions>(o => o.Dimensions == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        vectorStore.Verify(x => x.ReplaceNodeVectorsAsync(
            KnowledgeGraphId,
            "n1",
            It.Is<IReadOnlyList<KgEmbeddingVectorRecord>>(records =>
                records.Count == 1
                && records[0].NodeId == "n1"
                && records[0].KgId == KnowledgeGraphId
                && records[0].EntityTypeId == 5
                && records[0].Name == "名称"
                && records[0].Content == "名称\n描述"
                && records[0].Embedding.Length == 2
                && records[0].Key != Guid.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeltaAsync_DeleteNode_CallsDeleteNodeVectors()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        var sut = CreateSut(db.Context, vectorStore.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, DeleteNodeIds = ["n1"] }, CancellationToken.None);

        vectorStore.Verify(x => x.DeleteNodeVectorsAsync(KnowledgeGraphId, "n1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeltaAsync_WhenNodeMissing_SkipsWithoutVectorWrites()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNodeAsync(KnowledgeGraphId, "gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeGraphNodeRecord?)null);
        var sut = CreateSut(db.Context, vectorStore.Object, graphStore: graphStore.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["gone"] }, CancellationToken.None);

        vectorStore.Verify(x => x.ReplaceNodeVectorsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KgEmbeddingVectorRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStore.Verify(x => x.DeleteNodeVectorsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDeltaAsync_WhenNameAndDescriptionBlank_DeletesInsteadOfEmbedding()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNodeAsync(KnowledgeGraphId, "blank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("blank", KnowledgeGraphId, 5, " ", string.Empty));
        var generator = CreateGenerator();
        var sut = CreateSut(db.Context, vectorStore.Object, provider: CreateGeneratorProvider(generator.Object).Object, graphStore: graphStore.Object);

        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["blank"] }, CancellationToken.None);

        vectorStore.Verify(x => x.DeleteNodeVectorsAsync(KnowledgeGraphId, "blank", It.IsAny<CancellationToken>()), Times.Once);
        vectorStore.Verify(x => x.ReplaceNodeVectorsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KgEmbeddingVectorRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
        generator.Verify(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDeltaAsync_WhenSingleNodeFails_OthersStillProcessed()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNodeAsync(KnowledgeGraphId, "bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("bad", KnowledgeGraphId, 5, "坏节点", "描述"));
        graphStore
            .Setup(x => x.GetNodeAsync(KnowledgeGraphId, "good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("good", KnowledgeGraphId, 5, "好节点", "描述"));

        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .SetupSequence(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new[] { 0.1f, 0.2f })]));

        var sut = CreateSut(db.Context, vectorStore.Object, provider: CreateGeneratorProvider(generator.Object).Object, graphStore: graphStore.Object);

        // 单节点失败不影响其余节点，且不抛出（非全部失败不触发重投）
        await sut.ProcessDeltaAsync(new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["bad", "good"] }, CancellationToken.None);

        vectorStore.Verify(x => x.ReplaceNodeVectorsAsync(KnowledgeGraphId, "good", It.IsAny<IReadOnlyList<KgEmbeddingVectorRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
        vectorStore.Verify(x => x.ReplaceNodeVectorsAsync(KnowledgeGraphId, "bad", It.IsAny<IReadOnlyList<KgEmbeddingVectorRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDeltaAsync_AllUpsertFailed_Throws()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNodeAsync(KnowledgeGraphId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("n", KnowledgeGraphId, 5, "节点", "描述"));

        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("向量模型不可用"));

        var sut = CreateSut(db.Context, vectorStore.Object, provider: CreateGeneratorProvider(generator.Object).Object, graphStore: graphStore.Object);

        // 全部 upsert 失败：抛出最后异常触发 MQ 重投
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessDeltaAsync(
            new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, UpsertNodeIds = ["a", "b"] },
            CancellationToken.None));

        Assert.Equal("向量模型不可用", ex.Message);
        vectorStore.Verify(x => x.ReplaceNodeVectorsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KgEmbeddingVectorRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDeltaAsync_AllDeleteFailed_Throws()
    {
        using var db = await CreateDbAsync();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.DeleteNodeVectorsAsync(KnowledgeGraphId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("向量库不可用"));

        var sut = CreateSut(db.Context, vectorStore.Object);

        // 全部 delete 失败：抛出最后异常触发 MQ 重投
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessDeltaAsync(
            new KgNodeEmbeddingDeltaMessage { KgId = KnowledgeGraphId, DeleteNodeIds = ["a", "b"] },
            CancellationToken.None));

        Assert.Equal("向量库不可用", ex.Message);
    }

    private static KgEmbeddingService CreateSut(
        Database.DatabaseContext context,
        IKgEmbeddingVectorStore vectorStore,
        IEmbeddingGeneratorProvider? provider = null,
        IKnowledgeGraphStore? graphStore = null)
    {
        return new KgEmbeddingService(
            context,
            vectorStore,
            graphStore ?? Mock.Of<IKnowledgeGraphStore>(),
            provider ?? Mock.Of<IEmbeddingGeneratorProvider>(),
            NullLogger<KgEmbeddingService>.Instance);
    }

    private static Mock<IEmbeddingGeneratorProvider> CreateGeneratorProvider(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        var provider = new Mock<IEmbeddingGeneratorProvider>();
        provider
            .Setup(x => x.GetEmbeddingGeneratorAsync(It.IsAny<AiModelEntity>(), It.IsAny<AiChannelEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generator);
        return provider;
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateGenerator()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new[] { 0.1f, 0.2f })]));
        return generator;
    }

    private static async Task<TestSqliteContext> CreateDbAsync(bool withModel = true, bool isPublic = true, string mode = "managed")
    {
        var db = TestSqliteContext.Create();
        var now = DateTimeOffset.UtcNow;
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity
        {
            Id = KnowledgeGraphId,
            TeamId = 1,
            Name = "图谱",
            Description = string.Empty,
            Mode = mode,
            AvatarPath = string.Empty,
            EmbeddingModelId = withModel ? EmbeddingModelId : null,
            EmbeddingDimensions = withModel ? 2 : 0,
        });

        if (withModel)
        {
            db.Context.AiChannels.Add(new AiChannelEntity
            {
                Id = ChannelId,
                ProviderKey = "openai",
                Name = "桩渠道",
                ProtocolFamily = 1,
                BaseUrl = "http://127.0.0.1:9/v1",
                ApiKey = "sk-test",
                Enabled = true,
                CreateTime = now,
                UpdateTime = now,
                CreateUserId = 1,
                UpdateUserId = 1,
                IsDeleted = 0,
            });
            db.Context.AiModels.Add(new AiModelEntity
            {
                Id = EmbeddingModelId,
                ChannelId = ChannelId,
                ModelId = "text-embedding-3-small",
                Name = "桩向量化模型",
                ModelKind = "embedding",
                Enabled = true,
                IsPublic = isPublic,
                CreateTime = now,
                UpdateTime = now,
                CreateUserId = 1,
                UpdateUserId = 1,
                IsDeleted = 0,
            });
        }

        await db.Context.SaveChangesAsync(CancellationToken.None);
        return db;
    }
}
