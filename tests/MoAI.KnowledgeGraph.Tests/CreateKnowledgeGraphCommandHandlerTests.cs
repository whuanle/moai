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
        SetupStoreSeeding(store);
        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var result = await sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "物流", TemplateKey = "logistics" },
            CancellationToken.None);

        var entityTypes = db.Context.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == result.Value)
            .ToList();
        var portId = entityTypes.Single(x => x.Name == "港口").Id;
        var legId = entityTypes.Single(x => x.Name == "航段").Id;

        var relation = db.Context.KnowledgeGraphRelationTypes
            .Single(x => x.KnowledgeGraphId == result.Value && x.Name == "抵达");

        Assert.NotNull(relation.SourceTypeId);
        Assert.NotNull(relation.TargetTypeId);
        Assert.Equal(legId, relation.SourceTypeId);
        Assert.Equal(portId, relation.TargetTypeId);

        // 模板预置属性随实体类型一并落库
        var leg = entityTypes.Single(x => x.Name == "航段");
        var legProps = KnowledgeGraphPropertyJson.ParseDefinitions(leg.Properties);
        Assert.Contains(legProps, x => x.Name == "运输价格" && x.Type == KnowledgeGraphEntityTypeProperty.TypeNumber);
        Assert.Contains(legProps, x => x.Name == "距离公里" && x.Required);
    }

    [Fact]
    public async Task Handle_WithTemplate_SeedsExampleNodesAndEdges()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var createdNodes = new List<KnowledgeGraphNodeRecord>();
        var createdEdges = new List<(long RelationTypeId, string SourceNodeId, string TargetNodeId)>();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.CreateNodeAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((long kgId, long entityTypeId, string name, string description, string? propsJson, CancellationToken _) =>
                createdNodes.Add(new KnowledgeGraphNodeRecord(Guid.CreateVersion7().ToString(), kgId, entityTypeId, name, description, propsJson)))
            .Returns((long kgId, long entityTypeId, string name, string description, string? propsJson, CancellationToken _) =>
                Task.FromResult(createdNodes[^1]));
        store.Setup(x => x.CreateEdgeAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((long kgId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken _) =>
                createdEdges.Add((relationTypeId, sourceNodeId, targetNodeId)))
            .Returns((long kgId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken _) =>
                Task.FromResult(new KnowledgeGraphEdgeRecord(Guid.CreateVersion7().ToString(), kgId, relationTypeId, sourceNodeId, targetNodeId)));

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        var result = await sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "物流", TemplateKey = "logistics" },
            CancellationToken.None);

        var graphId = result.Value;

        // 模板预置 11 个示例实例、15 条示例关系一次性写入图库
        Assert.Equal(11, createdNodes.Count);
        Assert.All(createdNodes, x => Assert.Equal(graphId, x.KnowledgeGraphId));
        Assert.Equal(15, createdEdges.Count);

        // 航段节点携带预置距离/价格属性
        var shNingbo = createdNodes.Single(x => x.Name == "上海 → 宁波");
        var props = KnowledgeGraphPropertyJson.ParseValues(shNingbo.PropsJson);
        Assert.Equal("250", props["距离公里"]);
        Assert.Equal("800", props["运输价格"]);

        // 边端点映射正确：出发/抵达关联航段与港口，承运关联承运商与航段
        var entityTypes = db.Context.KnowledgeGraphEntityTypes.Where(x => x.KnowledgeGraphId == graphId).ToDictionary(x => x.Name, x => x.Id);
        var relationTypes = db.Context.KnowledgeGraphRelationTypes.Where(x => x.KnowledgeGraphId == graphId).ToDictionary(x => x.Name, x => x.Id);
        var nodeIdsByName = createdNodes.ToDictionary(x => x.Name, x => x.Id);
        var shNingboDepart = createdEdges.Single(x => x.SourceNodeId == shNingbo.Id && x.RelationTypeId == relationTypes["出发"]);
        Assert.Equal(nodeIdsByName["上海"], shNingboDepart.TargetNodeId);
        Assert.Equal(entityTypes["港口"], createdNodes.Single(x => x.Id == shNingboDepart.TargetNodeId).EntityTypeId);
        var coscoCarry = createdEdges.Single(x => x.SourceNodeId == nodeIdsByName["中远海运"] && x.TargetNodeId == nodeIdsByName["宁波 → 深圳"]);
        Assert.Equal(relationTypes["承运"], coscoCarry.RelationTypeId);
    }

    [Fact]
    public async Task Handle_WithTemplate_WhenSeedingFails_PurgesGraphAndRollsBack()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphStoreSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var store = new Mock<IKnowledgeGraphStore>();
        SetupStoreSeeding(store);
        // 节点全部写入成功、首条边写入失败 → 清理图库残留并整体回滚
        store.Setup(x => x.CreateEdgeAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("graph down"));

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object, store.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "物流", TemplateKey = "logistics" },
            CancellationToken.None));

        store.Verify(x => x.PurgeGraphAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(db.Context.KnowledgeGraphs);
        Assert.Empty(db.Context.KnowledgeGraphEntityTypes);
        Assert.Empty(db.Context.KnowledgeGraphRelationTypes);
    }

    private static void SetupStoreSeeding(Mock<IKnowledgeGraphStore> store)
    {
        store.Setup(x => x.CreateNodeAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((long kgId, long entityTypeId, string name, string description, string? propsJson, CancellationToken _) =>
                Task.FromResult(new KnowledgeGraphNodeRecord(Guid.CreateVersion7().ToString(), kgId, entityTypeId, name, description, propsJson)));
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
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "物流", TemplateKey = "logistics" },
            CancellationToken.None));

        Assert.Empty(db.Context.KnowledgeGraphs);
        Assert.Empty(db.Context.KnowledgeGraphEntityTypes);
        Assert.Empty(db.Context.KnowledgeGraphRelationTypes);
    }
}
