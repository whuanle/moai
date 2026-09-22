using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Commands;
using MoAI.App.Handlers;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// <see cref="SaveAppAgentConfigCommandHandler"/> 对知识图谱绑定（GraphIds）的校验与落库：
/// 空列表通过、非本团队/接入图 400、合法托管图通过（create/update 分支）.
/// </summary>
public class SaveAppAgentConfigCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithoutGraphIds_CreatesConfigWithEmptyGraphIds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var sut = CreateSut(db.Context);

        await sut.Handle(CreateCommand(appId), CancellationToken.None);

        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Equal("[]", config.GraphIds);
        Assert.Equal("[]", config.WikiIds);
    }

    [Fact]
    public async Task Handle_WhenGraphNotInTeam_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var foreignGraphId = SeedGraph(db.Context, teamId: 999, KnowledgeGraphModes.Managed);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, new[] { foreignGraphId }),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("知识图谱", ex.Message, StringComparison.Ordinal);
        Assert.Empty(db.Context.AppAgentConfigs);
    }

    [Fact]
    public async Task Handle_WhenGraphIsConnected_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var connectedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Connected);
        var sut = CreateSut(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, new[] { connectedGraphId }),
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Context.AppAgentConfigs);
    }

    [Fact]
    public async Task Handle_WhenTeamManagedGraph_SavesAndUpdatesGraphIds()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);
        var managedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Managed);
        var anotherManagedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Managed);
        var connectedGraphId = SeedGraph(db.Context, teamId: 7, KnowledgeGraphModes.Connected);
        var sut = CreateSut(db.Context);

        // create 分支：合法托管图落库，接入图/他团队图混入即整体拒绝
        await sut.Handle(CreateCommand(appId, new[] { managedGraphId }), CancellationToken.None);
        var config = db.Context.AppAgentConfigs.Single(x => x.AppId == appId);
        Assert.Equal($"[{managedGraphId}]", config.GraphIds);

        // 混入接入图：整体拒绝且原配置不被破坏
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            CreateCommand(appId, new[] { managedGraphId, connectedGraphId }),
            CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal($"[{managedGraphId}]", config.GraphIds);

        // update 分支：替换为另一张托管图 + 去重
        await sut.Handle(CreateCommand(appId, new[] { anotherManagedGraphId, anotherManagedGraphId }), CancellationToken.None);
        Assert.Equal($"[{anotherManagedGraphId}]", config.GraphIds);
    }

    private static SaveAppAgentConfigCommand CreateCommand(Guid appId, long[]? graphIds = null)
        => new()
        {
            AppId = appId,
            ContextUserId = 1,
            ModelId = null,
            WikiIds = Array.Empty<long>(),
            GraphIds = graphIds ?? Array.Empty<long>(),
            Plugins = Array.Empty<Guid>(),
        };

    private static SaveAppAgentConfigCommandHandler CreateSut(Database.DatabaseContext context)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);

        return new SaveAppAgentConfigCommandHandler(
            context,
            teamService.Object,
            Mock.Of<ISandboxSettingsService>());
    }

    private static Guid SeedApp(Database.DatabaseContext context)
    {
        var app = new AppEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "助手",
            Description = string.Empty,
            TeamId = 7,
            AppType = (int)AppType.Agent,
            Avatar = string.Empty,
        };
        context.Apps.Add(app);
        context.SaveChanges();
        return app.Id;
    }

    private static long SeedGraph(Database.DatabaseContext context, int teamId, string mode)
    {
        var graph = new KnowledgeGraphEntity
        {
            TeamId = teamId,
            Name = $"图-{Guid.NewGuid():N}",
            Description = string.Empty,
            Mode = mode,
            AvatarPath = string.Empty,
        };
        context.KnowledgeGraphs.Add(graph);
        context.SaveChanges();
        return graph.Id;
    }
}
