using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.App.Commands;
using MoAI.App.Handlers;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// 更新应用基础信息：版本号 aaa.bb.cc 解析为 int 落库、不携带保持原值、Member 403、非法格式 400.
/// </summary>
public class UpdateAppCommandHandlerTests
{
    [Fact]
    public async Task Handle_AdminUpdateVersion_ParsesToInt()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        await CreateSut(db.Context, TeamRole.Admin).Handle(new UpdateAppCommand
        {
            AppId = appId,
            Name = "助手",
            IsExternal = false,
            Version = "1.2.3",
        }, CancellationToken.None);

        var app = db.Context.Apps.Find(appId);
        Assert.NotNull(app);
        Assert.Equal(10203, app!.Version);
    }

    [Fact]
    public async Task Handle_VersionNotCarried_KeepsOriginal()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        await CreateSut(db.Context, TeamRole.Admin).Handle(new UpdateAppCommand
        {
            AppId = appId,
            Name = "助手改名",
            IsExternal = false,
        }, CancellationToken.None);

        var app = db.Context.Apps.Find(appId);
        Assert.NotNull(app);
        Assert.Equal(10000, app!.Version);
        Assert.Equal("助手改名", app.Name);
    }

    [Fact]
    public async Task Handle_InvalidVersionFormat_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Admin).Handle(
            new UpdateAppCommand { AppId = appId, Name = "助手", IsExternal = false, Version = "1.2.3.4" },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_Member_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Member).Handle(
            new UpdateAppCommand { AppId = appId, Name = "助手", IsExternal = false, Version = "2.0.0" },
            CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }

    private static UpdateAppCommandHandler CreateSut(Database.DatabaseContext context, TeamRole role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        return new UpdateAppCommandHandler(context, teamService.Object);
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
}
