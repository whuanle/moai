using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Commands;
using MoAI.Org.Handlers;
using MoAI.Settings.Commands;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 设置/清空默认角色单测.
/// </summary>
public class UpdateOrgRoleDefaultCommandHandlerTests
{
    [Fact]
    public async Task SetDefault_SavesRoleId()
    {
        using var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.SaveChanges();
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(x => x.SaveSettingAsync(It.IsAny<SaveSettingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyCommandResponse.Default);
        var handler = new UpdateOrgRoleDefaultCommandHandler(db.Context, settings.Object);

        var result = await handler.Handle(new UpdateOrgRoleDefaultCommand { OrgRoleId = 3 }, CancellationToken.None);

        Assert.Equal(EmptyCommandResponse.Default, result);
        settings.Verify(x => x.SaveSettingAsync(
            It.Is<SaveSettingCommand>(c => c.Key == MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey && c.Value == "3"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearDefault_SavesEmptyValue()
    {
        using var db = TestSqliteContext.Create();
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(x => x.SaveSettingAsync(It.IsAny<SaveSettingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyCommandResponse.Default);
        var handler = new UpdateOrgRoleDefaultCommandHandler(db.Context, settings.Object);

        await handler.Handle(new UpdateOrgRoleDefaultCommand { OrgRoleId = null }, CancellationToken.None);

        settings.Verify(x => x.SaveSettingAsync(
            It.Is<SaveSettingCommand>(c => c.Value == string.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetDefault_RoleMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = new UpdateOrgRoleDefaultCommandHandler(db.Context, Mock.Of<ISettingsService>());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleDefaultCommand { OrgRoleId = 999 }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }
}
