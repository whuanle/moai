using Microsoft.EntityFrameworkCore;
using MoAI.Infra.Exceptions;
using MoAI.Org.Commands;
using MoAI.Org.Handlers;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 删除组织角色单测：有子角色拒绝、叶子删除级联解绑用户.
/// </summary>
public class DeleteOrgRoleCommandHandlerTests
{
    [Fact]
    public async Task Delete_HasChildren_Throws400()
    {
        using var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(1, "集团A", null, "/1/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(2, "销售部", 1, "/1/2/"));
        await db.Context.SaveChangesAsync();
        var handler = new DeleteOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new DeleteOrgRoleCommand { OrgRoleId = 1 }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteLeaf_RemovesRoleAndUnbindsUsers()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 3));
        await db.Context.SaveChangesAsync();
        var handler = new DeleteOrgRoleCommandHandler(db.Context);

        await handler.Handle(new DeleteOrgRoleCommand { OrgRoleId = 3 }, CancellationToken.None);

        Assert.False(await db.Context.OrgRoles.AnyAsync(x => x.Id == 3));
        Assert.Equal(0, await db.Context.OrgUserRoles.CountAsync(x => x.RoleId == 3));
    }

    [Fact]
    public async Task Delete_RoleMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = new DeleteOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new DeleteOrgRoleCommand { OrgRoleId = 999 }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteDefaultRole_ClearsDefaultSetting()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 3));
        db.Context.Settings.Add(new MoAI.Database.Entities.SettingEntity
        {
            Key = MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey,
            Name = "组织默认角色",
            Description = string.Empty,
            Value = "3",
        });
        await db.Context.SaveChangesAsync();
        var handler = new DeleteOrgRoleCommandHandler(db.Context);

        await handler.Handle(new DeleteOrgRoleCommand { OrgRoleId = 3 }, CancellationToken.None);

        var setting = await db.Context.Settings.SingleAsync(x => x.Key == MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey);
        Assert.Equal(string.Empty, setting.Value);
        Assert.False(await db.Context.OrgUserRoles.AnyAsync(x => x.RoleId == 3));
    }

    [Fact]
    public async Task DeleteNonDefaultRole_KeepsDefaultSetting()
    {
        using var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.Settings.Add(new MoAI.Database.Entities.SettingEntity
        {
            Key = MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey,
            Name = "组织默认角色",
            Description = string.Empty,
            Value = "9",
        });
        await db.Context.SaveChangesAsync();
        var handler = new DeleteOrgRoleCommandHandler(db.Context);

        await handler.Handle(new DeleteOrgRoleCommand { OrgRoleId = 3 }, CancellationToken.None);

        var setting = await db.Context.Settings.SingleAsync(x => x.Key == MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey);
        Assert.Equal("9", setting.Value);
    }
}
