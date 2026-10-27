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
/// 更新应用基础信息：名称/描述/分类落库、版本号不再随基础信息修改（只能发布/上架时设置）、Member 403.
/// </summary>
public class UpdateAppCommandHandlerTests
{
    [Fact]
    public async Task Handle_AdminUpdate_KeepsVersionUntouched()
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
        Assert.Equal("助手改名", app!.Name);
        Assert.Equal(10000, app.Version);
    }

    [Fact]
    public async Task Handle_Member_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var appId = SeedApp(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSut(db.Context, TeamRole.Member).Handle(
            new UpdateAppCommand { AppId = appId, Name = "助手", IsExternal = false },
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
