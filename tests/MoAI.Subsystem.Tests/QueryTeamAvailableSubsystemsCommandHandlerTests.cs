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
/// 团队可用子系统查询处理器测试.
/// </summary>
public class QueryTeamAvailableSubsystemsCommandHandlerTests
{
    private static async Task<SubsystemEntity> SeedSubsystemAsync(DatabaseContext context, string key, SubsystemTeamScope scope, SubsystemStatus status = SubsystemStatus.Enabled)
    {
        var entity = new SubsystemEntity
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Name = key,
            Description = string.Empty,
            Secret = "hash",
            SecretSalt = "salt",
            Status = status,
            TeamScope = scope,
        };
        context.Subsystems.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private static QueryTeamAvailableSubsystemsCommandHandler CreateHandler(DatabaseContext context, TeamRole? role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        return new QueryTeamAvailableSubsystemsCommandHandler(context, teamService.Object);
    }

    [Fact]
    public async Task Handle_Member_SeesAllTeamsSubsystemsOnlyWhenEnabled()
    {
        using var db = TestSqliteContext.Create();
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 1);
        await SeedSubsystemAsync(db.Context, "all-ok", SubsystemTeamScope.AllTeams);
        await SeedSubsystemAsync(db.Context, "all-disabled", SubsystemTeamScope.AllTeams, SubsystemStatus.Disabled);
        await SeedSubsystemAsync(db.Context, "specified", SubsystemTeamScope.SpecifiedTeams);
        var handler = CreateHandler(db.Context, TeamRole.Member);

        var response = await handler.Handle(new QueryTeamAvailableSubsystemsCommand { TeamId = 1, ContextUserId = 10 }, CancellationToken.None);
        Assert.Equal(["all-ok"], response.Items.Select(x => x.Key).ToList());
    }

    [Fact]
    public async Task Handle_AuthorizedSpecifiedTeam_Visible()
    {
        using var db = TestSqliteContext.Create();
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 1);
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 2);
        var subsystem = await SeedSubsystemAsync(db.Context, "specified", SubsystemTeamScope.SpecifiedTeams);
        db.Context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = subsystem.Id, TeamId = 1 });
        await db.Context.SaveChangesAsync();
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
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 1);
        var subsystem = await SeedSubsystemAsync(db.Context, "specified", SubsystemTeamScope.SpecifiedTeams);
        db.Context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = subsystem.Id, TeamId = 1 });
        await db.Context.SaveChangesAsync();

        var onlineService = new SubsystemOnlineService(Mock.Of<StackExchange.Redis.Extensions.Core.Abstractions.IRedisDatabase>());
        var handler = new UpdateSubsystemCommandHandler(db.Context, onlineService);
        await handler.Handle(new UpdateSubsystemCommand
        {
            SubsystemId = subsystem.Id,
            Name = "new-name",
            Status = SubsystemStatus.Enabled,
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None);

        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == subsystem.Id).ToList());
        var updated = await db.Context.Subsystems.FindAsync(subsystem.Id);
        Assert.Equal("new-name", updated!.Name);
        Assert.Equal(SubsystemTeamScope.AllTeams, updated.TeamScope);
    }
}
