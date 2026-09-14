using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class CreateKnowledgeGraphCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEnabledAndAdmin_CreatesGraph()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var result = await sut.Handle(new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None);

        Assert.True(result.Value > 0);
        var graph = db.Context.KnowledgeGraphs.Single(x => x.Id == result.Value);
        Assert.Equal(KnowledgeGraphModes.Managed, graph.Mode);
        Assert.Null(graph.Database);
    }

    [Fact]
    public async Task Handle_Connected_WhenProbeFails_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.ProbeDatabaseAsync("ext", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "外部图", Mode = KnowledgeGraphModes.Connected, Database = "ext" },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Context.KnowledgeGraphs);
    }

    [Fact]
    public async Task Handle_Connected_WhenProbeSucceeds_PersistsModeAndDatabase()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.ProbeDatabaseAsync("ext", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var result = await sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "外部图", Mode = KnowledgeGraphModes.Connected, Database = " ext " },
            CancellationToken.None);

        var graph = db.Context.KnowledgeGraphs.Single(x => x.Id == result.Value);
        Assert.Equal(KnowledgeGraphModes.Connected, graph.Mode);
        Assert.Equal("ext", graph.Database);
        Assert.Null(graph.TemplateKey);
    }

    [Fact]
    public async Task Handle_Connected_WhenNameDuplicated_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.ProbeDatabaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        await sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "外部图", Mode = KnowledgeGraphModes.Connected, Database = "ext" },
            CancellationToken.None);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "外部图", Mode = KnowledgeGraphModes.Connected, Database = "ext2" },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Single(db.Context.KnowledgeGraphs);
    }

    [Fact]
    public async Task Handle_WhenDisabled_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = false });

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WhenEnabledButUriEmpty_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = " " });

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WhenNameDuplicated_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Owner);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        await sut.Handle(new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WithTemplate_MapsRelationTypeIdsToSavedEntityTypes()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var result = await sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "运维", TemplateKey = "ops" },
            CancellationToken.None);

        var entityTypes = db.Context.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == result.Value)
            .ToList();
        var peopleId = entityTypes.Single(x => x.Name == "人员").Id;
        var serviceId = entityTypes.Single(x => x.Name == "服务").Id;

        var relation = db.Context.KnowledgeGraphRelationTypes
            .Single(x => x.KnowledgeGraphId == result.Value && x.Name == "维护");

        Assert.NotNull(relation.SourceTypeId);
        Assert.NotNull(relation.TargetTypeId);
        Assert.Equal(peopleId, relation.SourceTypeId);
        Assert.Equal(serviceId, relation.TargetTypeId);
    }

    [Fact]
    public async Task Handle_WithTemplate_WhenRelationTypeInsertFails_RollsBackGraphAndEntityTypes()
    {
        using var db = TestSqliteContext.Create();

        // 用 sqlite 触发器强制关系类型落库失败，验证前面已保存的图谱与实体类型一并回滚
        // 注意：测试上下文走 EF 约定表名（未应用 Postgres 命名配置），故表名为 DbSet 名称
        await db.Context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_relation_type_insert BEFORE INSERT ON KnowledgeGraphRelationTypes BEGIN SELECT RAISE(ABORT, 'forced failure'); END;");

        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
        var store = new Mock<IKnowledgeGraphStore>();

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "运维", TemplateKey = "ops" },
            CancellationToken.None));

        Assert.Empty(db.Context.KnowledgeGraphs);
        Assert.Empty(db.Context.KnowledgeGraphEntityTypes);
        Assert.Empty(db.Context.KnowledgeGraphRelationTypes);
    }
}
