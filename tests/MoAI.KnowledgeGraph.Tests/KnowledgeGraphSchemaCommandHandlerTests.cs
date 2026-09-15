using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphSchemaCommandHandlerTests
{
    private const long KnowledgeGraphId = 7;

    [Fact]
    public async Task CreateRelationType_WithValidSourceAndTarget_Creates()
    {
        using var db = TestSqliteContext.Create();
        var people = new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        var service = new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
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
                KnowledgeGraphId = KnowledgeGraphId,
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
                KnowledgeGraphId = KnowledgeGraphId,
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
            KnowledgeGraphId = KnowledgeGraphId,
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
        store.Setup(x => x.CountNodesByEntityTypeAsync(KnowledgeGraphId, entityType.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = new DeleteKnowledgeGraphEntityTypeCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new DeleteKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = KnowledgeGraphId, EntityTypeId = entityType.Id },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteEntityType_ReferencedByRelationType_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var entityType = new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        db.Context.KnowledgeGraphEntityTypes.Add(entityType);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        db.Context.KnowledgeGraphRelationTypes.Add(new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "维护",
            Color = string.Empty,
            Description = string.Empty,
            SourceTypeId = entityType.Id,
            Sort = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var settings = CreateSettings();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.CountNodesByEntityTypeAsync(KnowledgeGraphId, entityType.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = new DeleteKnowledgeGraphEntityTypeCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new DeleteKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = KnowledgeGraphId, EntityTypeId = entityType.Id },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateEntityType_WhenSettingsDisabled_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = CreateAuthorizer();
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = false });

        var sut = new CreateKnowledgeGraphEntityTypeCommandHandler(db.Context, authorizer.Object, settings.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = KnowledgeGraphId, Name = "人员" },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateEntityType_WithDuplicatedName_Throws409()
    {
        using var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphEntityTypes.Add(new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var settings = CreateSettings();
        var sut = new CreateKnowledgeGraphEntityTypeCommandHandler(db.Context, authorizer.Object, settings.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphEntityTypeCommand { KnowledgeGraphId = KnowledgeGraphId, Name = "人员" },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateRelationType_WithNonExistentSourceType_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var relationType = new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "维护",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        db.Context.KnowledgeGraphRelationTypes.Add(relationType);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var settings = CreateSettings();
        var sut = new UpdateKnowledgeGraphRelationTypeCommandHandler(db.Context, authorizer.Object, settings.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new UpdateKnowledgeGraphRelationTypeCommand
            {
                KnowledgeGraphId = KnowledgeGraphId,
                RelationTypeId = relationType.Id,
                Name = "维护",
                SourceTypeId = 999,
            },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task QuerySchema_ReturnsOrderedWithMappedFields()
    {
        using var db = TestSqliteContext.Create();
        var service = new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "服务",
            Color = "#123456",
            Description = "服务描述",
            Sort = 0,
        };
        var people = new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = "#abcdef",
            Description = "人员描述",
            Sort = 1,
        };
        db.Context.KnowledgeGraphEntityTypes.Add(service);
        db.Context.KnowledgeGraphEntityTypes.Add(people);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var depends = new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "依赖",
            Color = "#111111",
            Description = "依赖关系",
            Sort = 0,
        };
        var maintain = new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "维护",
            Color = "#222222",
            Description = "维护关系",
            SourceTypeId = people.Id,
            TargetTypeId = service.Id,
            Sort = 1,
        };
        db.Context.KnowledgeGraphRelationTypes.Add(depends);
        db.Context.KnowledgeGraphRelationTypes.Add(maintain);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var sut = new QueryKnowledgeGraphSchemaCommandHandler(db.Context, authorizer.Object, CreateIntrospectionCache().Object);

        var response = await sut.Handle(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = KnowledgeGraphId }, CancellationToken.None);

        Assert.Equal(2, response.EntityTypes.Count);
        Assert.Equal(service.Id, response.EntityTypes[0].EntityTypeId);
        Assert.Equal("服务", response.EntityTypes[0].Name);
        Assert.Equal("#123456", response.EntityTypes[0].Color);
        Assert.Equal("服务描述", response.EntityTypes[0].Description);
        Assert.Equal(people.Id, response.EntityTypes[1].EntityTypeId);
        Assert.Equal("人员", response.EntityTypes[1].Name);

        Assert.Equal(2, response.RelationTypes.Count);
        Assert.Equal(depends.Id, response.RelationTypes[0].RelationTypeId);
        Assert.Equal("依赖", response.RelationTypes[0].Name);
        Assert.Equal("#111111", response.RelationTypes[0].Color);
        Assert.Equal("依赖关系", response.RelationTypes[0].Description);
        Assert.Null(response.RelationTypes[0].SourceTypeId);
        Assert.Null(response.RelationTypes[0].TargetTypeId);
        Assert.Equal(maintain.Id, response.RelationTypes[1].RelationTypeId);
        Assert.Equal(people.Id, response.RelationTypes[1].SourceTypeId);
        Assert.Equal(service.Id, response.RelationTypes[1].TargetTypeId);
    }

    [Fact]
    public async Task QuerySchema_WhenConnected_UsesIntrospection()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(KnowledgeGraphId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KnowledgeGraphId, TeamId = 1, Name = "外部图", Mode = KnowledgeGraphModes.Connected, Database = "ext", AvatarPath = string.Empty }, MoAI.Database.Enums.TeamRole.Admin));

        var introspection = new KnowledgeGraphIntrospection(
            new List<KnowledgeGraphIntrospectedItem> { new("Person", 3) },
            new List<KnowledgeGraphIntrospectedItem> { new("KNOWS", 2) },
            new List<string> { "name", "age" });
        var cache = new Mock<IKnowledgeGraphIntrospectionCache>();
        cache.Setup(x => x.GetAsync(KnowledgeGraphId, "ext", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((introspection, (KnowledgeGraphIntrospectionDiff?)null, false));

        var sut = new QueryKnowledgeGraphSchemaCommandHandler(db.Context, authorizer.Object, cache.Object);

        var response = await sut.Handle(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = KnowledgeGraphId }, CancellationToken.None);

        Assert.Equal(KnowledgeGraphModes.Connected, response.Mode);
        Assert.True(response.ReadOnly);
        Assert.Equal("ext", response.Database);
        var entityType = Assert.Single(response.EntityTypes);
        Assert.Null(entityType.EntityTypeId);
        Assert.Equal("Person", entityType.Name);
        Assert.Equal(3, entityType.Count);
        var relationType = Assert.Single(response.RelationTypes);
        Assert.Null(relationType.RelationTypeId);
        Assert.Equal("KNOWS", relationType.Name);
        Assert.Equal(2, relationType.Count);
        Assert.Equal(new[] { "name", "age" }, response.PropertyKeys);
        cache.Verify(x => x.GetAsync(KnowledgeGraphId, "ext", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QuerySchema_WhenConnectedWithRefresh_ForwardsRefresh()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(KnowledgeGraphId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KnowledgeGraphId, TeamId = 1, Name = "外部图", Mode = KnowledgeGraphModes.Connected, Database = "ext", AvatarPath = string.Empty }, MoAI.Database.Enums.TeamRole.Admin));

        var cache = new Mock<IKnowledgeGraphIntrospectionCache>();
        cache.Setup(x => x.GetAsync(KnowledgeGraphId, "ext", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphIntrospection(
                new List<KnowledgeGraphIntrospectedItem> { new("Person", 3) },
                new List<KnowledgeGraphIntrospectedItem>(),
                new List<string>()),
                new KnowledgeGraphIntrospectionDiff(new List<string> { "Person" }, new List<string>(), new List<string>(), new List<string>()),
                false));

        var sut = new QueryKnowledgeGraphSchemaCommandHandler(db.Context, authorizer.Object, cache.Object);

        var response = await sut.Handle(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = KnowledgeGraphId, Refresh = true }, CancellationToken.None);

        Assert.NotNull(response.Changes);
        Assert.Equal(new[] { "Person" }, response.Changes!.AddedLabels);
        Assert.True(response.Changes.IsEmpty is false);
        cache.Verify(x => x.GetAsync(KnowledgeGraphId, "ext", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IKnowledgeGraphAuthorizer> CreateAuthorizer()
    {
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KnowledgeGraphId, TeamId = 1, Name = "图谱" }, MoAI.Database.Enums.TeamRole.Admin));
        authorizer.Setup(x => x.AuthorizeManagedAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KnowledgeGraphId, TeamId = 1, Name = "图谱" }, MoAI.Database.Enums.TeamRole.Admin));
        return authorizer;
    }

    [Fact]
    public async Task QuerySchema_ReturnsEntityTypeProperties()
    {
        using var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphEntityTypes.Add(new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Properties = "[{\"name\":\"年龄\",\"type\":\"number\",\"required\":true,\"description\":\"周岁\"}]",
            Sort = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var sut = new QueryKnowledgeGraphSchemaCommandHandler(db.Context, authorizer.Object, CreateIntrospectionCache().Object);

        var response = await sut.Handle(new QueryKnowledgeGraphSchemaCommand { KnowledgeGraphId = KnowledgeGraphId }, CancellationToken.None);

        var entityType = Assert.Single(response.EntityTypes);
        var prop = Assert.Single(entityType.Properties);
        Assert.Equal("年龄", prop.Name);
        Assert.Equal("number", prop.Type);
        Assert.True(prop.Required);
        Assert.Equal("周岁", prop.Description);
    }

    private static Mock<IKnowledgeGraphIntrospectionCache> CreateIntrospectionCache()
    {
        return new Mock<IKnowledgeGraphIntrospectionCache>();
    }

    private static Mock<IKnowledgeGraphSettingsService> CreateSettings()
    {
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        return settings;
    }
}
