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

public class UpdateKnowledgeGraphAvatarCommandHandlerTests
{
    private const long KnowledgeGraphId = 9;

    private static async Task<TestSqliteContext> CreateGraphAsync()
    {
        var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity
        {
            Id = KnowledgeGraphId,
            TeamId = 1,
            Name = "图谱",
            Description = string.Empty,
            Mode = "managed",
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

    [Fact]
    public async Task Handle_WithUnregisteredObjectKey_Throws404()
    {
        using var db = await CreateGraphAsync();
        var graph = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        var sut = new UpdateKnowledgeGraphAvatarCommandHandler(db.Context, CreateAuthorizer(graph!));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new UpdateKnowledgeGraphAvatarCommand { KnowledgeGraphId = KnowledgeGraphId, ObjectKey = "ghost/key.png" },
            CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WithRegisteredFile_SavesAvatarPath()
    {
        using var db = await CreateGraphAsync();
        var graph = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        db.Context.Files.Add(new FileEntity
        {
            ObjectKey = "kg/avatar.png",
            IsUploaded = true,
            FileExtension = ".png",
            FileSha256 = "0123456789abcdef",
            FileSize = 1024,
            ContentType = "image/png",
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);
        var sut = new UpdateKnowledgeGraphAvatarCommandHandler(db.Context, CreateAuthorizer(graph!));

        await sut.Handle(
            new UpdateKnowledgeGraphAvatarCommand { KnowledgeGraphId = KnowledgeGraphId, ObjectKey = "kg/avatar.png" },
            CancellationToken.None);

        var saved = await db.Context.KnowledgeGraphs.FindAsync(KnowledgeGraphId);
        Assert.Equal("kg/avatar.png", saved!.AvatarPath);
    }
}
