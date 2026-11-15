using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Org.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using Moq;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 用户面门户：当前用户可进入的子系统列表查询处理器测试（启用 + 已开放登录 + 团队∪组织授权）.
/// </summary>
public class QueryMySubsystemsCommandHandlerTests
{
    private static QueryMySubsystemsCommandHandler CreateHandler(DatabaseContext context, bool orgAllowed = false)
    {
        var orgGrants = new Mock<IOrgGrantService>();
        orgGrants.Setup(x => x.IsAllowedAsync(It.IsAny<OrgGrantResourceType>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgAllowed);
        return new QueryMySubsystemsCommandHandler(context, orgGrants.Object);
    }

    private static async Task SeedMemberAsync(DatabaseContext context, int teamId, long userId)
    {
        context.TeamUsers.Add(new TeamUserEntity { TeamId = teamId, UserId = userId });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_FiltersDisabledNoCallbackAndAppRows()
    {
        using var db = TestSqliteContext.Create();
        await SeedMemberAsync(db.Context, 1, 10);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "visible", callback: "https://report.local/cb");
        await TestSeeds.SeedSubsysAppAsync(db.Context, "disabled", status: AccessAppStatus.Disabled, callback: "https://report.local/cb");
        // 未配置回调=未开放委托登录，无法进入，不下发门户
        await TestSeeds.SeedSubsysAppAsync(db.Context, "no-callback");
        // 应用接入行不属于子系统门户
        await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-AppRow", AccessAppType.App, callback: "https://erp.local/cb");

        var response = await CreateHandler(db.Context).Handle(new QueryMySubsystemsCommand { ContextUserId = 10 }, CancellationToken.None);

        var item = Assert.Single(response.Items);
        Assert.Equal("visible", item.Key);
        Assert.Equal("visible", item.Name);
    }

    [Fact]
    public async Task Handle_SpecifiedTeams_OnlyAuthorizedTeamSees()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        TestSeeds.SeedTeam(db.Context, 2);
        await TestSeeds.SeedSubsysAppAsync(
            db.Context,
            "specified",
            scope: SubsystemTeamScope.SpecifiedTeams,
            callback: "https://report.local/cb",
            teamIds: [1]);
        await SeedMemberAsync(db.Context, 2, 10);

        var denied = await CreateHandler(db.Context).Handle(new QueryMySubsystemsCommand { ContextUserId = 10 }, CancellationToken.None);
        Assert.Empty(denied.Items);

        await SeedMemberAsync(db.Context, 1, 10);
        var allowed = await CreateHandler(db.Context).Handle(new QueryMySubsystemsCommand { ContextUserId = 10 }, CancellationToken.None);
        Assert.Equal("specified", Assert.Single(allowed.Items).Key);
    }

    [Fact]
    public async Task Handle_NoTeam_ButOrgGrant_Visible()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "org-only", callback: "https://report.local/cb");

        var response = await CreateHandler(db.Context, orgAllowed: true).Handle(
            new QueryMySubsystemsCommand { ContextUserId = 10 },
            CancellationToken.None);

        Assert.Equal("org-only", Assert.Single(response.Items).Key);
    }

    [Fact]
    public async Task Handle_Anonymous_ReturnsEmpty()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "visible", callback: "https://report.local/cb");

        var response = await CreateHandler(db.Context).Handle(new QueryMySubsystemsCommand(), CancellationToken.None);

        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task Handle_ReturnsAvatarPath()
    {
        using var db = TestSqliteContext.Create();
        await SeedMemberAsync(db.Context, 1, 10);
        var subsystem = await TestSeeds.SeedSubsysAppAsync(db.Context, "with-avatar", callback: "https://report.local/cb");
        subsystem.AvatarPath = "public/avatar/subsys.png";
        await db.Context.SaveChangesAsync();

        var response = await CreateHandler(db.Context).Handle(new QueryMySubsystemsCommand { ContextUserId = 10 }, CancellationToken.None);

        Assert.Equal("public/avatar/subsys.png", Assert.Single(response.Items).AvatarPath);
    }
}
