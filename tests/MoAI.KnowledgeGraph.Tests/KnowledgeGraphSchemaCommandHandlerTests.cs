using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphSchemaCommandHandlerTests
{
    private const long KgId = 7;

    [Fact]
    public async Task CreateRelationType_WithValidSourceAndTarget_Creates()
    {
        using var db = TestSqliteContext.Create();
        var people = new KnowledgeGraphEntityTypeEntity
        {
            KgId = KgId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        var service = new KnowledgeGraphEntityTypeEntity
        {
            KgId = KgId,
            Name = "服务",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 1,
        };
        db.Context.KnowledgeGraphEntityTypes.Add(people);
        db.Context.KnowledgeGraphEntityTypes.Add(service);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var settings = CreateSettings();
        var sut = new CreateKnowledgeGraphRelationTypeCommandHandler(db.Context, authorizer.Object, settings.Object);

        var result = await sut.Handle(
            new CreateKnowledgeGraphRelationTypeCommand
            {
                KgId = KgId,
                Name = "维护",
                SourceTypeId = people.Id,
                TargetTypeId = service.Id,
            },
            CancellationToken.None);

        Assert.True(result.Value > 0);
        var saved = await db.Context.KnowledgeGraphRelationTypes.FindAsync(result.Value);
        Assert.NotNull(saved);
        Assert.Equal(people.Id, saved!.SourceTypeId);
        Assert.Equal(service.Id, saved.TargetTypeId);
    }

    [Fact]
    public async Task CreateRelationType_WithNonExistentSourceType_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = CreateAuthorizer();
        var settings = CreateSettings();
        var sut = new CreateKnowledgeGraphRelationTypeCommandHandler(db.Context, authorizer.Object, settings.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphRelationTypeCommand
            {
                KgId = KgId,
                Name = "维护",
                SourceTypeId = 999,
            },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteEntityType_WithNodesPresent_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var entityType = new KnowledgeGraphEntityTypeEntity
        {
            KgId = KgId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        db.Context.KnowledgeGraphEntityTypes.Add(entityType);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var settings = CreateSettings();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.CountNodesByEntityTypeAsync(KgId, entityType.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = new DeleteKnowledgeGraphEntityTypeCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new DeleteKnowledgeGraphEntityTypeCommand { KgId = KgId, EntityTypeId = entityType.Id },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    private static Mock<IKnowledgeGraphAuthorizer> CreateAuthorizer()
    {
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KgId, TeamId = 1, Name = "图谱" }, MoAI.Database.Enums.TeamRole.Admin));
        return authorizer;
    }

    private static Mock<IKnowledgeGraphSettingsService> CreateSettings()
    {
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        return settings;
    }
}
