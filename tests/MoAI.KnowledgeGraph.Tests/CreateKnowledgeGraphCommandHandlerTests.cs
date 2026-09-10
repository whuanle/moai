using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

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
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
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
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });
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
    public async Task Handle_WhenDisabled_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = false });

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
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = " " });

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
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

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
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var result = await sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "运维", TemplateKey = "ops" },
            CancellationToken.None);

        var entityTypes = db.Context.KnowledgeGraphEntityTypes
            .Where(x => x.KgId == result.Value)
            .ToList();
        var peopleId = entityTypes.Single(x => x.Name == "人员").Id;
        var serviceId = entityTypes.Single(x => x.Name == "服务").Id;

        var relation = db.Context.KnowledgeGraphRelationTypes
            .Single(x => x.KgId == result.Value && x.Name == "维护");

        Assert.NotNull(relation.SourceTypeId);
        Assert.NotNull(relation.TargetTypeId);
        Assert.Equal(peopleId, relation.SourceTypeId);
        Assert.Equal(serviceId, relation.TargetTypeId);
    }
}
