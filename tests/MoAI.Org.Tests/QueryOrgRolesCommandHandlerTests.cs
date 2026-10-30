using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Org.Handlers;
using MoAI.Org.Queries;
using Moq;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 组织角色查询单测：path 层级序、成员计数、按用户/按角色两向查询.
/// </summary>
public class QueryOrgRolesCommandHandlerTests
{
    private static void Seed(MoAI.Database.DatabaseContext context)
    {
        context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        context.Users.Add(OrgTestSeed.User(101, "lisi", "李四"));
        context.OrgRoles.Add(OrgTestSeed.Role(1, "集团A", null, "/1/"));
        context.OrgRoles.Add(OrgTestSeed.Role(2, "销售部", 1, "/1/2/"));
        context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        context.OrgRoles.Add(OrgTestSeed.Role(12, "西南组", 1, "/1/12/"));
        context.OrgRoles.Add(OrgTestSeed.Role(10, "集团B", null, "/10/"));
        context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 2));
        context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 3));
        context.OrgUserRoles.Add(OrgTestSeed.Binding(101, 1));
        context.Settings.Add(new MoAI.Database.Entities.SettingEntity
        {
            Key = MoAI.Database.Seed.SettingDefinitions.OrgDefaultRoleKey,
            Name = "组织默认角色",
            Description = string.Empty,
            Value = "2",
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task List_ReturnsPathOrderWithCounts()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new QueryOrgRolesCommandHandler(db.Context, Mock.Of<IUserInfoFillService>());

        var result = await handler.Handle(new QueryOrgRolesCommand(), CancellationToken.None);

        // 数值层级序：1 的子树（2→3、12）在前，根 10 在后；纯字符串序会把 /10/ 排到 /1/2/ 前
        Assert.Equal([1, 2, 3, 12, 10], result.Items.Select(x => x.OrgRoleId).ToArray());
        var sales = result.Items.Single(x => x.OrgRoleId == 2);
        Assert.True(sales.HasChildren);
        Assert.Equal(1, sales.UserCount);
        Assert.True(sales.IsDefault);
        var east = result.Items.Single(x => x.OrgRoleId == 3);
        Assert.False(east.HasChildren);
        Assert.False(east.IsDefault);
    }

    [Fact]
    public async Task List_SearchByName_Filters()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new QueryOrgRolesCommandHandler(db.Context, Mock.Of<IUserInfoFillService>());

        var result = await handler.Handle(new QueryOrgRolesCommand { Search = "销售" }, CancellationToken.None);

        Assert.Equal([2], result.Items.Select(x => x.OrgRoleId).ToArray());
    }

    [Fact]
    public async Task RoleUsers_ReturnsBindingsWithUserInfo()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new QueryOrgRoleUsersCommandHandler(db.Context, Mock.Of<IUserInfoFillService>());

        var result = await handler.Handle(new QueryOrgRoleUsersCommand { RoleId = 2 }, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(100, item.UserId);
        Assert.Equal("zhangsan", item.UserName);
        Assert.Equal("张三", item.NickName);
        Assert.Equal("zhangsan@test.local", item.Email);
        Assert.False(item.IsAdmin);
        Assert.False(item.IsRoot);
        Assert.False(item.IsDisable);
    }

    [Fact]
    public async Task RoleUsers_SearchByNickName_Filters()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(101, 2));
        db.Context.SaveChanges();
        var handler = new QueryOrgRoleUsersCommandHandler(db.Context, Mock.Of<IUserInfoFillService>());

        var result = await handler.Handle(new QueryOrgRoleUsersCommand { RoleId = 2, Search = "李四" }, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(101, item.UserId);
    }

    [Fact]
    public async Task RoleUsers_RoleMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new QueryOrgRoleUsersCommandHandler(db.Context, Mock.Of<IUserInfoFillService>());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new QueryOrgRoleUsersCommand { RoleId = 999 }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UserRoles_ReturnsPathOrderWithUserInfo()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new QueryUserOrgRolesCommandHandler(db.Context);

        var result = await handler.Handle(new QueryUserOrgRolesCommand { UserId = 100 }, CancellationToken.None);

        Assert.Equal("zhangsan", result.UserName);
        Assert.Equal([2, 3], result.Items.Select(x => x.OrgRoleId).ToArray());
        Assert.Equal("/1/2/", result.Items[0].Path);
    }

    [Fact]
    public async Task UserRoles_UserMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new QueryUserOrgRolesCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new QueryUserOrgRolesCommand { UserId = 999 }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }
}
