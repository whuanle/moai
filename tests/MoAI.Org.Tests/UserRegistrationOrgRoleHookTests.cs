using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.Org.Core.Services;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 注册默认角色钩子单测：入列语义、无默认/角色缺失/已有绑定跳过.
/// </summary>
public class UserRegistrationOrgRoleHookTests
{
    private static void SeedSetting(MoAI.Database.DatabaseContext context, string value)
    {
        context.Settings.Add(new SettingEntity
        {
            Key = SettingDefinitions.OrgDefaultRoleKey,
            Name = "组织默认角色",
            Description = string.Empty,
            Value = value,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task DefaultSet_EnqueuesBinding_AndCallerSavePersists()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        SeedSetting(db.Context, "3");
        var hook = new UserRegistrationOrgRoleHook(db.Context, NullLogger<UserRegistrationOrgRoleHook>.Instance);

        await hook.OnUserRegisteredAsync(100, CancellationToken.None);

        // 钩子只入列，由注册主流程统一保存
        Assert.True(db.Context.ChangeTracker.HasChanges());
        await db.Context.SaveChangesAsync();
        Assert.True(await db.Context.OrgUserRoles.AnyAsync(x => x.UserId == 100 && x.RoleId == 3));
    }

    [Fact]
    public async Task NoSettingRow_DoesNothing()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.SaveChanges();
        var hook = new UserRegistrationOrgRoleHook(db.Context, NullLogger<UserRegistrationOrgRoleHook>.Instance);

        await hook.OnUserRegisteredAsync(100, CancellationToken.None);

        Assert.False(db.Context.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public async Task EmptyOrInvalidValue_DoesNothing(string value)
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        SeedSetting(db.Context, value);
        var hook = new UserRegistrationOrgRoleHook(db.Context, NullLogger<UserRegistrationOrgRoleHook>.Instance);

        await hook.OnUserRegisteredAsync(100, CancellationToken.None);

        Assert.False(db.Context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task RoleMissing_SilentlySkips()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        SeedSetting(db.Context, "999");
        var hook = new UserRegistrationOrgRoleHook(db.Context, NullLogger<UserRegistrationOrgRoleHook>.Instance);

        await hook.OnUserRegisteredAsync(100, CancellationToken.None);

        Assert.False(db.Context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ExistingBinding_Skips()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 3));
        SeedSetting(db.Context, "3");
        db.Context.SaveChanges();
        var hook = new UserRegistrationOrgRoleHook(db.Context, NullLogger<UserRegistrationOrgRoleHook>.Instance);

        await hook.OnUserRegisteredAsync(100, CancellationToken.None);

        Assert.False(db.Context.ChangeTracker.HasChanges());
    }
}
