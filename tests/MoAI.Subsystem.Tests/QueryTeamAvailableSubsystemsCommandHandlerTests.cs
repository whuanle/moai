using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 团队可用子系统查询处理器测试（仅 appType=subsystem 行可见）.
/// </summary>
public class QueryTeamAvailableSubsystemsCommandHandlerTests
{
    private static QueryTeamAvailableSubsystemsCommandHandler CreateHandler(DatabaseContext context, TeamRole? role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        return new QueryTeamAvailableSubsystemsCommandHandler(context, teamService.Object);
    }

    [Fact]
    public async Task Handle_Member_SeesEnabledAllTeamsSubsystemOnly()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "all-ok", scope: SubsystemTeamScope.AllTeams);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "all-disabled", scope: SubsystemTeamScope.AllTeams, status: AccessAppStatus.Disabled);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "specified", scope: SubsystemTeamScope.SpecifiedTeams);
        // 应用接入行不属于团队可用子系统
        await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-AppRow", AccessAppType.App);
        var handler = CreateHandler(db.Context, TeamRole.Member);

        var response = await handler.Handle(new QueryTeamAvailableSubsystemsCommand { TeamId = 1, ContextUserId = 10 }, CancellationToken.None);
        Assert.Equal(["all-ok"], response.Items.Select(x => x.Key).ToList());
    }

    [Fact]
    public async Task Handle_AuthorizedSpecifiedTeam_Visible()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        TestSeeds.SeedTeam(db.Context, 2);
        var subsystem = await TestSeeds.SeedSubsysAppAsync(db.Context, "specified", scope: SubsystemTeamScope.SpecifiedTeams, teamIds: [1]);
        var handler = CreateHandler(db.Context, TeamRole.Member);

        var team1 = await handler.Handle(new QueryTeamAvailableSubsystemsCommand { TeamId = 1, ContextUserId = 10 }, CancellationToken.None);
        var team2 = await handler.Handle(new QueryTeamAvailableSubsystemsCommand { TeamId = 2, ContextUserId = 10 }, CancellationToken.None);
        Assert.Equal(["specified"], team1.Items.Select(x => x.Key).ToList());
        Assert.Empty(team2.Items);
    }

    [Fact]
    public async Task Handle_NonMember_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = CreateHandler(db.Context, null);

        var ex = await Assert.ThrowsAsync<MoAI.Infra.Exceptions.BusinessException>(() =>
            handler.Handle(new QueryTeamAvailableSubsystemsCommand { TeamId = 1, ContextUserId = 10 }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_UpdateToAllTeams_ClearsAuths()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        var subsystem = await TestSeeds.SeedSubsysAppAsync(db.Context, "specified", scope: SubsystemTeamScope.SpecifiedTeams, teamIds: [1]);

        var handler = new UpdateSubsysAppCommandHandler(db.Context, Mock.Of<ISubsystemOnlineService>());
        await handler.Handle(new UpdateSubsysAppCommand
        {
            Id = subsystem.Id,
            Name = "new-name",
            Status = AccessAppStatus.Enabled,
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None);

        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == subsystem.Id).ToList());
        var updated = await db.Context.AccessApps.FindAsync(subsystem.Id);
        Assert.Equal("new-name", updated!.Name);
        Assert.Equal(SubsystemTeamScope.AllTeams, updated.TeamScope);
    }
}
