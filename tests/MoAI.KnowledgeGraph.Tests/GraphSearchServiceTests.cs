using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class GraphSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenInputInvalid_ReturnsEmptyWithoutTouchingVectorStore()
    {
        using var db = TestSqliteContext.Create();
        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, vectorStore.Object);

        foreach (var (ids, query) in new[]
                 {
                     (new long[] { 1, 2 }, "   "),
                     (Array.Empty<long>(), "查询"),
                     (new long[] { -1, 0 }, "查询"),
                 })
        {
            var result = await sut.SearchAsync(ids, query, 5, null, CancellationToken.None);

            Assert.Empty(result.Hits);
            Assert.Empty(result.Contents);
            Assert.Empty(result.SkippedHints);
            Assert.Equal(string.Empty, result.Text);
        }

        vectorStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchAsync_WhenGraphNotConfigured_SkipsWithHintAndSearchesOtherGraphs()
    {
        using var db = TestSqliteContext.Create();
        await SeedGraphAsync(db, 1, modelId: null, dimensions: 0);
        var modelId2 = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 2, modelId2);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(2, It.IsAny<ReadOnlyMemory<float>>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult> { Hit(2, "n1", "甲", "甲描述", 0.9) });

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object);

        var result = await sut.SearchAsync(new long[] { 1, 2 }, "查询", 2, null, CancellationToken.None);

        var hint = Assert.Single(result.SkippedHints);
        Assert.Contains("图谱 1", hint, StringComparison.Ordinal);
        Assert.Contains("未配置向量化模型", hint, StringComparison.Ordinal);

        var hit = Assert.Single(result.Hits);
        Assert.Equal(2, hit.KgId);
        Assert.Equal("n1", hit.NodeId);

        vectorStore.Verify(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStore.Verify(x => x.SearchAsync(2, It.IsAny<ReadOnlyMemory<float>>(), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WhenGraphConnected_SkipsWithHintWithoutVectorSearch()
    {
        using var db = TestSqliteContext.Create();
        var modelId1 = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 1, modelId1, mode: KnowledgeGraphModes.Connected);
        var modelId2 = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 2, modelId2);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(2, It.IsAny<ReadOnlyMemory<float>>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult> { Hit(2, "n1", "甲", "甲描述", 0.9) });

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object);

        var result = await sut.SearchAsync(new long[] { 1, 2 }, "查询", 2, null, CancellationToken.None);

        var hint = Assert.Single(result.SkippedHints);
        Assert.Contains("图谱 1", hint, StringComparison.Ordinal);
        Assert.Contains("接入图谱", hint, StringComparison.Ordinal);

        var hit = Assert.Single(result.Hits);
        Assert.Equal(2, hit.KgId);

        vectorStore.Verify(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStore.Verify(x => x.SearchAsync(2, It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_MergesHitsAcrossGraphsByScoreDescendingAndCapsTotal()
    {
        using var db = TestSqliteContext.Create();
        var modelId1 = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 1, modelId1);
        var modelId2 = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 2, modelId2);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult>
            {
                Hit(1, "n1", "甲", "甲描述", 0.9),
                Hit(1, "n2", "乙", "乙描述", 0.7),
            });
        vectorStore
            .Setup(x => x.SearchAsync(2, It.IsAny<ReadOnlyMemory<float>>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult>
            {
                Hit(2, "m1", "丙", "丙描述", 0.8),
                Hit(2, "m2", "丁", "丁描述", 0.6),
            });

        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNeighborsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<KnowledgeGraphNodeRecord>(), Array.Empty<KnowledgeGraphEdgeRecord>(), false));

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object, graphStore.Object);

        var result = await sut.SearchAsync(new long[] { 1, 2 }, "查询", 2, null, CancellationToken.None);

        Assert.Empty(result.SkippedHints);
        Assert.Equal(4, result.Hits.Count);
        Assert.Equal(new[] { "n1", "m1", "n2", "m2" }, result.Hits.Select(x => x.NodeId).ToArray());
        Assert.Equal(new long[] { 1, 2, 1, 2 }, result.Hits.Select(x => x.KgId).ToArray());
        Assert.Equal(new double?[] { 0.9, 0.8, 0.7, 0.6 }, result.Hits.Select(x => x.Score).ToArray());

        // Contents 与 Hits 同序
        Assert.Equal(new[] { "甲：甲描述", "丙：丙描述", "乙：乙描述", "丁：丁描述" }, result.Contents.ToArray());
        Assert.False(string.IsNullOrEmpty(result.Text));
    }

    [Fact]
    public async Task SearchAsync_MinScore_FiltersLowAndNullScores()
    {
        using var db = TestSqliteContext.Create();
        var modelId = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 1, modelId);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult>
            {
                Hit(1, "n1", "甲", "甲描述", 0.9),
                Hit(1, "n2", "乙", "乙描述", 0.5),
                Hit(1, "n3", "丙", "丙描述", null),
            });

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object);

        // 有阈值：低分与 null 得分均被过滤
        var filtered = await sut.SearchAsync(new long[] { 1 }, "查询", 5, 0.8, CancellationToken.None);
        var hit = Assert.Single(filtered.Hits);
        Assert.Equal("n1", hit.NodeId);
        Assert.Equal(0.9, hit.Score);

        // 无阈值：null 得分保留且排在最后
        var all = await sut.SearchAsync(new long[] { 1 }, "查询", 5, null, CancellationToken.None);
        Assert.Equal(new[] { "n1", "n2", "n3" }, all.Hits.Select(x => x.NodeId).ToArray());
        Assert.Null(all.Hits[2].Score);
    }

    [Fact]
    public async Task SearchAsync_BuildsNeighborsWithDirectionRelationNameAndTypeName()
    {
        using var db = TestSqliteContext.Create();
        var modelId = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 1, modelId);
        await SeedTypesAsync(
            db,
            1,
            new[] { (Id: 101L, Name: "人物") },
            new[] { (Id: 201L, Name: "认识"), (Id: 202L, Name: "属于") });

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult> { Hit(1, "n1", 101, "张三", "张三描述", 0.9) });

        IReadOnlyList<KnowledgeGraphNodeRecord> nodes = new List<KnowledgeGraphNodeRecord>
        {
            new("n2", 1, 101, "李四", "李四描述"),
            new("n3", 1, 101, "王五", "王五描述"),
        };
        IReadOnlyList<KnowledgeGraphEdgeRecord> edges = new List<KnowledgeGraphEdgeRecord>
        {
            new("e1", 1, 201, "n1", "n2"),          // 出边：n1 → 李四
            new("e2", 1, 202, "n3", "n1"),          // 入边：王五 → n1
            new("e3", 1, 201, "n1", "ghost"),       // 邻居不在返回节点集：跳过
            new("e4", 1, 999, "n1", "n3"),          // 关系类型未定义：RelationName 为 null
        };
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNeighborsAsync(1, "n1", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((nodes, edges, false));

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object, graphStore.Object);

        var result = await sut.SearchAsync(new long[] { 1 }, "查询", 5, null, CancellationToken.None);

        graphStore.Verify(x => x.GetNeighborsAsync(1, "n1", 10, It.IsAny<CancellationToken>()), Times.Once);

        var hit = Assert.Single(result.Hits);
        Assert.Equal("人物", hit.EntityTypeName);
        Assert.Equal(3, hit.Neighbors.Count);

        Assert.Equal("认识", hit.Neighbors[0].RelationName);
        Assert.Equal("out", hit.Neighbors[0].Direction);
        Assert.Equal("李四", hit.Neighbors[0].Name);
        Assert.Equal("李四描述", hit.Neighbors[0].Description);

        Assert.Equal("属于", hit.Neighbors[1].RelationName);
        Assert.Equal("in", hit.Neighbors[1].Direction);
        Assert.Equal("王五", hit.Neighbors[1].Name);
        Assert.Equal("王五描述", hit.Neighbors[1].Description);

        Assert.Null(hit.Neighbors[2].RelationName);
        Assert.Equal("out", hit.Neighbors[2].Direction);
        Assert.Equal("王五", hit.Neighbors[2].Name);

        Assert.Contains("【张三（人物）】张三描述", result.Text, StringComparison.Ordinal);
        Assert.Contains("  └─ 认识(out)→ 李四：李四描述", result.Text, StringComparison.Ordinal);
        Assert.Contains("  └─ 属于(in)→ 王五：王五描述", result.Text, StringComparison.Ordinal);
        Assert.Contains("  └─ 关联(out)→ 王五：王五描述", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("ghost", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_WhenTextTooLong_TruncatesWithSuffix()
    {
        using var db = TestSqliteContext.Create();
        var modelId = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 1, modelId);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult>
            {
                Hit(1, "n1", "节点", new string('长', 9000), 0.9),
            });

        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNeighborsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<KnowledgeGraphNodeRecord>(), Array.Empty<KnowledgeGraphEdgeRecord>(), false));

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object, graphStore.Object);

        var result = await sut.SearchAsync(new long[] { 1 }, "查询", 5, null, CancellationToken.None);

        Assert.EndsWith("…(已截断)", result.Text, StringComparison.Ordinal);
        Assert.Equal(8192 + "…(已截断)".Length, result.Text.Length);
    }

    [Fact]
    public async Task SearchAsync_WhenModelUnavailable_SkipsWithHintAndSearchesOtherGraphs()
    {
        using var db = TestSqliteContext.Create();

        // 模型非公开且无团队授权：解析失败
        await SeedEmbeddingModelAsync(db, isPublic: false);
        await SeedGraphAsync(db, 1, db.Context.AiModels.Single().Id);
        var modelId2 = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 2, modelId2);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>();
        vectorStore
            .Setup(x => x.SearchAsync(2, It.IsAny<ReadOnlyMemory<float>>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KgEmbeddingSearchResult> { Hit(2, "n1", "甲", "甲描述", 0.9) });

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(CreateGenerator().Object).Object);

        var result = await sut.SearchAsync(new long[] { 1, 2 }, "查询", 2, null, CancellationToken.None);

        var hint = Assert.Single(result.SkippedHints);
        Assert.Contains("图谱 1", hint, StringComparison.Ordinal);
        Assert.Contains("向量化模型不可用或未授权", hint, StringComparison.Ordinal);

        var hit = Assert.Single(result.Hits);
        Assert.Equal(2, hit.KgId);

        vectorStore.Verify(x => x.SearchAsync(1, It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WhenQueryVectorEmpty_SkipsGraphWithHint()
    {
        using var db = TestSqliteContext.Create();
        var modelId = await SeedEmbeddingModelAsync(db);
        await SeedGraphAsync(db, 1, modelId);

        var vectorStore = new Mock<IKgEmbeddingVectorStore>(MockBehavior.Strict);
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([]));

        var sut = CreateSut(db.Context, vectorStore.Object, CreateGeneratorProvider(generator.Object).Object);

        var result = await sut.SearchAsync(new long[] { 1 }, "查询", 5, null, CancellationToken.None);

        Assert.Empty(result.Hits);
        var hint = Assert.Single(result.SkippedHints);
        Assert.Contains("图谱 1", hint, StringComparison.Ordinal);
        Assert.Contains("查询向量生成失败", hint, StringComparison.Ordinal);
        vectorStore.VerifyNoOtherCalls();
    }

    private static GraphSearchService CreateSut(
        DatabaseContext context,
        IKgEmbeddingVectorStore vectorStore,
        IEmbeddingGeneratorProvider? provider = null,
        IKnowledgeGraphStore? graphStore = null)
    {
        return new GraphSearchService(
            context,
            provider ?? Mock.Of<IEmbeddingGeneratorProvider>(),
            vectorStore,
            graphStore ?? CreateEmptyNeighborGraphStore());
    }

    private static IKnowledgeGraphStore CreateEmptyNeighborGraphStore()
    {
        var graphStore = new Mock<IKnowledgeGraphStore>();
        graphStore
            .Setup(x => x.GetNeighborsAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<KnowledgeGraphNodeRecord>(), Array.Empty<KnowledgeGraphEdgeRecord>(), false));
        return graphStore.Object;
    }

    private static Mock<IEmbeddingGeneratorProvider> CreateGeneratorProvider(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        var provider = new Mock<IEmbeddingGeneratorProvider>();
        provider
            .Setup(x => x.GetEmbeddingGeneratorAsync(It.IsAny<AiModelEntity>(), It.IsAny<AiChannelEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generator);
        return provider;
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateGenerator(int dimensions = 2)
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[dimensions])]));
        return generator;
    }

    private static KgEmbeddingSearchResult Hit(long kgId, string nodeId, string name, string description, double? score)
        => Hit(kgId, nodeId, 0, name, description, score);

    private static KgEmbeddingSearchResult Hit(long kgId, string nodeId, long entityTypeId, string name, string description, double? score)
        => new()
        {
            Record = new KgEmbeddingVectorRecord
            {
                Key = Guid.CreateVersion7(),
                KgId = kgId,
                NodeId = nodeId,
                EntityTypeId = entityTypeId,
                Name = name,
                Content = $"{name}\n{description}",
            },
            Score = score,
        };

    private static async Task SeedGraphAsync(TestSqliteContext db, long id, Guid? modelId, int dimensions = 2, string mode = KnowledgeGraphModes.Managed, int teamId = 1)
    {
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity
        {
            Id = id,
            TeamId = teamId,
            Name = $"图谱{id}",
            Description = string.Empty,
            Mode = mode,
            AvatarPath = string.Empty,
            EmbeddingModelId = modelId,
            EmbeddingDimensions = dimensions,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<Guid> SeedEmbeddingModelAsync(TestSqliteContext db, bool enabled = true, bool isPublic = true)
    {
        var modelId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        db.Context.AiChannels.Add(new AiChannelEntity
        {
            Id = channelId,
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
            Id = modelId,
            ChannelId = channelId,
            ModelId = "text-embedding-3-small",
            Name = "桩向量化模型",
            ModelKind = "embedding",
            Enabled = enabled,
            IsPublic = isPublic,
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
        return modelId;
    }

    private static async Task SeedTypesAsync(TestSqliteContext db, long kgId, (long Id, string Name)[] entityTypes, (long Id, string Name)[] relationTypes)
    {
        foreach (var (id, name) in entityTypes)
        {
            db.Context.KnowledgeGraphEntityTypes.Add(new KnowledgeGraphEntityTypeEntity
            {
                Id = id,
                KnowledgeGraphId = kgId,
                Name = name,
                Color = string.Empty,
                Description = string.Empty,
                Sort = 0,
                Properties = "[]",
            });
        }

        foreach (var (id, name) in relationTypes)
        {
            db.Context.KnowledgeGraphRelationTypes.Add(new KnowledgeGraphRelationTypeEntity
            {
                Id = id,
                KnowledgeGraphId = kgId,
                Name = name,
                Color = string.Empty,
                Description = string.Empty,
                Sort = 0,
            });
        }

        await db.Context.SaveChangesAsync(CancellationToken.None);
    }
}
