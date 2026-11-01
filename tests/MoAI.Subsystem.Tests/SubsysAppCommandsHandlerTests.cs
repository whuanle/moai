using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using Moq;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 系统级接入 CRUD 处理器测试：更新按类型分字段、软删、列表回显（子系统与应用接入同表）.
/// </summary>
public class SubsysAppCommandsHandlerTests
{
    [Fact]
    public async Task Update_AppRow_IgnoresSubsystemFields()
    {
        using var db = TestSqliteContext.Create();
        var created = await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-App", MoAI.Database.Enums.AccessAppType.App);
        var handler = new UpdateSubsysAppCommandHandler(db.Context, Mock.Of<ISubsystemOnlineService>());

        await handler.Handle(new UpdateSubsysAppCommand
        {
            Id = created.Id,
            Name = "b",
            Description = "desc",
            Status = AccessAppStatus.Disabled,
            CallbackUrls = ["https://erp.local/cb"],
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [1],
        }, CancellationToken.None);

        var entity = await db.Context.AccessApps.FindAsync(created.Id);
        Assert.Equal("b", entity!.Name);
        Assert.Equal(AccessAppStatus.Disabled, entity.Status);
        // 应用接入行不受子系统专属字段影响
        Assert.Equal("[]", entity.CallbackUrls);
        Assert.Equal(SubsystemTeamScope.AllTeams, entity.TeamScope);
        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == entity.Id).ToList());
    }

    [Fact]
    public async Task Update_SubsystemRow_UpdatesCallbackAndCoversTeams()
    {
        using var db = TestSqliteContext.Create();
        TestSeeds.SeedTeam(db.Context, 1);
        TestSeeds.SeedTeam(db.Context, 2);
        var created = await TestSeeds.SeedSubsysAppAsync(db.Context, "report", teamIds: [1]);
        var handler = new UpdateSubsysAppCommandHandler(db.Context, Mock.Of<ISubsystemOnlineService>());

        await handler.Handle(new UpdateSubsysAppCommand
        {
            Id = created.Id,
            Name = "new-name",
            Status = AccessAppStatus.Enabled,
            TeamScope = SubsystemTeamScope.SpecifiedTeams,
            TeamIds = [2],
            CallbackUrls = ["https://new.local/cb"],
        }, CancellationToken.None);

        var entity = await db.Context.AccessApps.FindAsync(created.Id);
        Assert.Equal("new-name", entity!.Name);
        Assert.Equal("[\"https://new.local/cb\"]", entity.CallbackUrls);
        Assert.Equal([2], db.Context.SubsystemTeamAuths
            .Where(x => x.SubsystemId == entity.Id)
            .Select(x => x.TeamId).ToList());

        // 全员范围清空授权行
        await handler.Handle(new UpdateSubsysAppCommand
        {
            Id = created.Id,
            Name = "new-name",
            Status = AccessAppStatus.Enabled,
            TeamScope = SubsystemTeamScope.AllTeams,
        }, CancellationToken.None);
        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == entity.Id).ToList());
    }

    [Fact]
    public async Task Update_Missing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = new UpdateSubsysAppCommandHandler(db.Context, Mock.Of<ISubsystemOnlineService>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new UpdateSubsysAppCommand
        {
            Id = Guid.NewGuid(),
            Name = "x",
        }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ResetSecret_ReplacesWithReversibleFormat_AndResetsRateLimit()
    {
        using var db = TestSqliteContext.Create();
        var cipher = TestSeeds.CreateCipher();
        var created = await TestSeeds.SeedSubsysAppAsync(db.Context, "report");
        var rateLimiter = new Mock<ISubsysTokenRateLimiter>();
        var handler = new ResetSubsysAppSecretCommandHandler(db.Context, cipher, rateLimiter.Object);

        var response = await handler.Handle(new ResetSubsysAppSecretCommand { Id = created.Id }, CancellationToken.None);
        Assert.Contains("****", response.SecretMask, StringComparison.Ordinal);

        var entity = await db.Context.AccessApps.FindAsync(created.Id);
        Assert.StartsWith("v2:", entity!.Secret, StringComparison.Ordinal);
        Assert.True(cipher.TryReveal(entity.Secret, out var secret));
        Assert.Equal(48, secret.Length);
        Assert.Equal(cipher.Mask(secret), response.SecretMask);

        // 新密钥换取不被旧失败计数误伤：重置后清限流窗口
        rateLimiter.Verify(x => x.ResetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndRemovesAuths()
    {
        using var db = TestSqliteContext.Create();
        var created = await TestSeeds.SeedSubsysAppAsync(db.Context, "report", teamIds: [1]);
        var rateLimiter = new Mock<ISubsysTokenRateLimiter>();
        var handler = new DeleteSubsysAppCommandHandler(db.Context, Mock.Of<ISubsystemOnlineService>(), rateLimiter.Object);

        await handler.Handle(new DeleteSubsysAppCommand { Id = created.Id }, CancellationToken.None);
        rateLimiter.Verify(x => x.ResetAllAsync(), Times.Once);

        // 软删后走数据库查询（避开 FindAsync 的跟踪缓存）应不可见（appid 立即失效）
        Assert.Null(await db.Context.AccessApps.AsNoTracking().FirstOrDefaultAsync(x => x.Id == created.Id));
        Assert.Empty(db.Context.SubsystemTeamAuths.Where(x => x.SubsystemId == created.Id).ToList());
    }

    [Fact]
    public async Task Query_ListsBothTypes_WithSearchAndTypeFilter()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await TestSeeds.SeedSubsysAppAsync(db.Context, "report", teamIds: [1]);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "门户", MoAI.Database.Enums.AccessAppType.App);
        TestSeeds.SeedTeam(db.Context, 1);

        var onlineService = new Mock<ISubsystemOnlineService>();
        onlineService.Setup(x => x.GetOnlineInstanceCountAsync(subsystem.Id)).ReturnsAsync(2);
        var handler = new QuerySubsysAppsCommandHandler(db.Context, onlineService.Object, Mock.Of<IUserInfoFillService>());

        var all = await handler.Handle(new QuerySubsysAppsCommand(), CancellationToken.None);
        Assert.Equal(2, all.Items.Count);
        var subsystemItem = all.Items.Single(x => x.Id == subsystem.Id);
        Assert.Equal(MoAI.Database.Enums.AccessAppType.Subsystem, subsystemItem.AppType);
        Assert.Equal(["team-1"], subsystemItem.AuthorizedTeams.Select(x => x.TeamName).ToList());
        Assert.True(subsystemItem.Online);
        Assert.Equal(2, subsystemItem.OnlineInstances);

        var appItem = all.Items.Single(x => x.AppType == MoAI.Database.Enums.AccessAppType.App);
        Assert.Empty(appItem.AuthorizedTeams);
        Assert.False(appItem.Online);

        var filtered = await handler.Handle(new QuerySubsysAppsCommand { Search = "门户" }, CancellationToken.None);
        Assert.Single(filtered.Items);

        var appsOnly = await handler.Handle(new QuerySubsysAppsCommand { AppType = MoAI.Database.Enums.AccessAppType.App }, CancellationToken.None);
        Assert.Equal([MoAI.Database.Enums.AccessAppType.App], appsOnly.Items.Select(x => x.AppType).ToList());
    }
}
