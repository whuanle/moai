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
/// 发布应用：携带版本号时必须严格大于当前版本并快照到线上版本、不携带时快照原值、非法格式 400、Member 403.
/// </summary>
public class PublishAppCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithVersion_UpdatesVersionAndSnapshot()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        await CreateSut(db.Context, TeamRole.Admin).Handle(
            new PublishAppCommand { AppId = appId, Version = "1.2.3" },
            CancellationToken.None);

        var app = db.Context.Apps.Find(appId);
        Assert.NotNull(app);
        Assert.Equal(10203, app!.Version);
        Assert.Equal(10203, app.PublishVersion);
        Assert.Equal(1, app.PublishStatus);
    }

    [Fact]
    public async Task Handle_WithoutVersion_SnapshotsOriginal()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        await CreateSut(db.Context, TeamRole.Admin).Handle(
            new PublishAppCommand { AppId = appId },
            CancellationToken.None);

        var app = db.Context.Apps.Find(appId);
        Assert.NotNull(app);
        Assert.Equal(10000, app!.Version);
        Assert.Equal(10000, app.PublishVersion);
        Assert.Equal(1, app.PublishStatus);
    }

    [Fact]
    public async Task Handle_InvalidVersionFormat_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Admin).Handle(
            new PublishAppCommand { AppId = appId, Version = "1.2.3.4" },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_LowerVersion_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Admin).Handle(
            new PublishAppCommand { AppId = appId, Version = "0.0.1" },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        var app = db.Context.Apps.Find(appId);
        Assert.NotNull(app);
        Assert.Equal(10000, app!.Version);
        Assert.Equal(0, app.PublishStatus);
    }

    [Fact]
    public async Task Handle_EqualVersion_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Admin).Handle(
            new PublishAppCommand { AppId = appId, Version = "1.0.0" },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        var app = db.Context.Apps.Find(appId);
        Assert.NotNull(app);
        Assert.Equal(10000, app!.Version);
        Assert.Equal(0, app.PublishStatus);
    }

    [Fact]
    public async Task Handle_Member_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Member).Handle(
            new PublishAppCommand { AppId = appId, Version = "2.0.0" },
            CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }

    private static PublishAppCommandHandler CreateSut(Database.DatabaseContext context, TeamRole role)
    {
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        return new PublishAppCommandHandler(context, teamService.Object);
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
