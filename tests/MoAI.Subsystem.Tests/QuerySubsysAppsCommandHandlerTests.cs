using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 系统级接入列表查询处理器测试：授权团队/组织名称回显与类型过滤.
/// </summary>
public class QuerySubsysAppsCommandHandlerTests
{
    private static QuerySubsysAppsCommandHandler CreateHandler(DatabaseContext context)
    {
        // 在线服务走 mock（默认离线 0 实例），Redis 语义由 E2E 覆盖
        var onlineService = new Mock<ISubsystemOnlineService>();
        return new QuerySubsysAppsCommandHandler(context, onlineService.Object, Mock.Of<IUserInfoFillService>());
    }

    [Fact]
    public async Task Handle_SpecifiedTeams_ReturnsAuthorizedTeamNames()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        TestSeeds.SeedTeam(db.Context, 2);
        // 团队 99 不存在：授权行仍在（历史数据），名称应回退 #99
        await TestSeeds.SeedSubsysAppAsync(db.Context, "all", scope: SubsystemTeamScope.AllTeams);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "specified", scope: SubsystemTeamScope.SpecifiedTeams, teamIds: [2, 1, 99]);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-AppRow", MoAI.Database.Enums.AccessAppType.App);

        var handler = CreateHandler(db.Context);
        var response = await handler.Handle(new QuerySubsysAppsCommand(), CancellationToken.None);

        Assert.Equal(3, response.Items.Count);

        var specified = response.Items.Single(x => x.Key == "specified");
        Assert.Equal(3, specified.AuthorizedTeamCount);
        // 按团队 id 升序，名称取自团队表（种子命名为 team-{id}）；已删除团队回退 #id
        Assert.Equal(["team-1", "team-2", "#99"], specified.AuthorizedTeams.Select(x => x.TeamName).ToList());
        Assert.Equal([1, 2, 99], specified.AuthorizedTeams.Select(x => x.TeamId).ToList());

        var all = response.Items.Single(x => x.Key == "all");
        Assert.Empty(all.AuthorizedTeams);
        Assert.Equal(0, all.AuthorizedTeamCount);

        // 应用接入行无授权信息
        var appItem = response.Items.Single(x => x.AppType == AccessAppType.App);
        Assert.Empty(appItem.AuthorizedTeams);
        Assert.Empty(appItem.AuthorizedOrgs);
    }

    [Fact]
    public async Task Handle_ReturnsAuthorizedOrgsWithScopeAndFallbackName()
    {
        using var db = TestSqliteContext.Create();
        var entity = await TestSeeds.SeedSubsysAppAsync(db.Context, "with-org", scope: SubsystemTeamScope.AllTeams);

        db.Context.OrgRoles.Add(new OrgRoleEntity { Id = 5, Name = "经理", ParentId = null, Path = "/5/" });
        db.Context.OrgGrants.Add(new OrgGrantEntity
        {
            ResourceType = (int)OrgGrantResourceType.Subsystem,
            ResourceId = entity.Id.ToString(),
            OrgRoleId = 5,
            Scope = (int)OrgGrantScope.Down,
        });
        // 角色已删除场景：授权行仍在，名称应回退 #99
        db.Context.OrgGrants.Add(new OrgGrantEntity
        {
            ResourceType = (int)OrgGrantResourceType.Subsystem,
            ResourceId = entity.Id.ToString(),
            OrgRoleId = 99,
            Scope = (int)OrgGrantScope.Self,
        });
        await db.Context.SaveChangesAsync();

        var handler = CreateHandler(db.Context);
        var response = await handler.Handle(new QuerySubsysAppsCommand(), CancellationToken.None);

        var item = response.Items.Single(x => x.Key == "with-org");
        Assert.Equal([5, 99], item.AuthorizedOrgs.Select(x => x.OrgRoleId).ToList());
        Assert.Equal(["经理", "#99"], item.AuthorizedOrgs.Select(x => x.OrgName).ToList());
        Assert.Equal(OrgGrantScope.Down, item.AuthorizedOrgs[0].Scope);
        Assert.Equal(OrgGrantScope.Self, item.AuthorizedOrgs[1].Scope);
    }

    [Fact]
    public async Task Handle_TypeFilter_NarrowsToRequestedType()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "report");
        await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-AppRow", AccessAppType.App);

        var handler = CreateHandler(db.Context);
        var appsOnly = await handler.Handle(new QuerySubsysAppsCommand { AppType = AccessAppType.App }, CancellationToken.None);
        Assert.Equal([AccessAppType.App], appsOnly.Items.Select(x => x.AppType).ToList());

        var subsystemsOnly = await handler.Handle(new QuerySubsysAppsCommand { AppType = AccessAppType.Subsystem }, CancellationToken.None);
        Assert.Equal([AccessAppType.Subsystem], subsystemsOnly.Items.Select(x => x.AppType).ToList());
    }
}
