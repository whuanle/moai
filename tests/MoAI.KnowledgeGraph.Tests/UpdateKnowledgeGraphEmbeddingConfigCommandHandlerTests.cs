using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests
{
    private const long KnowledgeGraphId = 11;

    private static readonly Guid ModelId = Guid.NewGuid();

    private static async Task<TestSqliteContext> CreateGraphAsync(string mode = "managed")
    {
        var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity
        {
            Id = KnowledgeGraphId,
            TeamId = 1,
            Name = "图谱",
            Description = string.Empty,
            Mode = mode,
            AvatarPath = string.Empty,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
        return db;
    }

    private static IKnowledgeGraphAuthorizer CreateAuthorizer(KnowledgeGraphEntity graph)
    {
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(KnowledgeGraphId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((graph, MoAI.Database.Enums.TeamRole.Admin));
        return authorizer.Object;
    }

    private static async Task SeedModelAsync(TestSqliteContext db, bool enabled = true, bool isPublic = true)
    {
        var channel = new AiChannelEntity
        {
            Id = Guid.NewGuid(),
            ProviderKey = "openai",
            Name = "桩渠道",
            ProtocolFamily = 1,
            BaseUrl = "http://127.0.0.1:9/v1",
            ApiKey = "sk-test",
            Enabled = true,
        };
        db.Context.AiChannels.Add(channel);
        db.Context.AiModels.Add(new AiModelEntity
        {
            Id = ModelId,
            ChannelId = channel.Id,
            ModelId = "text-embedding-3-small",
            Name = "桩向量化模型",
            ModelKind = "embedding",
            Enabled = enabled,
            IsPublic = isPublic,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handle_NonManagedGraph_Throws409()
    {
        using var db = await CreateGraphAsync(mode: "connected");
        var graph = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        var sut = new UpdateKnowledgeGraphEmbeddingConfigCommandHandler(db.Context, CreateAuthorizer(graph!));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new UpdateKnowledgeGraphEmbeddingConfigCommand { KnowledgeGraphId = KnowledgeGraphId, EmbeddingModelId = ModelId, EmbeddingDimensions = 1024 },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_ModelNotAuthorized_Throws400()
    {
        using var db = await CreateGraphAsync();
        var graph = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        await SeedModelAsync(db, enabled: true, isPublic: false);
        var sut = new UpdateKnowledgeGraphEmbeddingConfigCommandHandler(db.Context, CreateAuthorizer(graph!));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new UpdateKnowledgeGraphEmbeddingConfigCommand { KnowledgeGraphId = KnowledgeGraphId, EmbeddingModelId = ModelId, EmbeddingDimensions = 1024 },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_ValidModel_SavesConfig()
    {
        using var db = await CreateGraphAsync();
        var graph = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        await SeedModelAsync(db, enabled: true, isPublic: true);
        var sut = new UpdateKnowledgeGraphEmbeddingConfigCommandHandler(db.Context, CreateAuthorizer(graph!));

        await sut.Handle(
            new UpdateKnowledgeGraphEmbeddingConfigCommand { KnowledgeGraphId = KnowledgeGraphId, EmbeddingModelId = ModelId, EmbeddingDimensions = 1536 },
            CancellationToken.None);

        var saved = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        Assert.Equal(ModelId, saved!.EmbeddingModelId);
        Assert.Equal(1536, saved.EmbeddingDimensions);
    }
}
