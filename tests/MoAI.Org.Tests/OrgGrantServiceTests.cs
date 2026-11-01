using Microsoft.EntityFrameworkCore;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Org.Commands;
using MoAI.Org.Core.Services;
using MoAI.Org.Handlers;
using MoAI.Org.Services;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 统一组织授权单测：方向规则语义（self/up/down）、并集、整体覆盖、树迁移重判、删除角色级联.
/// 测试树：1 集团(/1/) → 2 销售部(/1/2/) → 3 华东组(/1/2/3/)；1 → 4 研发部(/1/4/)。
/// 用户绑定：100→[3]，101→[2]，102→[1]，103→[3,4]。
/// </summary>
public class OrgGrantServiceTests
{
    private static (MoAI.Database.DatabaseContext Context, IOrgGrantService Service) Create()
    {
        var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(1, "集团", null, "/1/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(2, "销售部", 1, "/1/2/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(4, "研发部", 1, "/1/4/"));
        db.Context.Users.Add(OrgTestSeed.User(100, "u100", "用户100"));
        db.Context.Users.Add(OrgTestSeed.User(101, "u101", "用户101"));
        db.Context.Users.Add(OrgTestSeed.User(102, "u102", "用户102"));
        db.Context.Users.Add(OrgTestSeed.User(103, "u103", "用户103"));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 3));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(101, 2));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(102, 1));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(103, 3));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(103, 4));
        db.Context.SaveChanges();
        return (db.Context, new OrgGrantService(db.Context, new InMemoryOrgGrantCache()));
    }

    private static OrgGrantItem Grant(int roleId, OrgGrantScope scope) => new() { OrgRoleId = roleId, Scope = scope };

    [Fact]
    public async Task NoGrant_Denies()
    {
        var (context, service) = Create();
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));
        Assert.Empty(await service.GetGrantsAsync(OrgGrantResourceType.Wiki, 800));
    }

    [Fact]
    public async Task ScopeSelf_OnlyExactRole()
    {
        var (context, service) = Create();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Self)]);

        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 102));
    }

    [Fact]
    public async Task ScopeUp_IncludesAncestors()
    {
        var (context, service) = Create();
        // 报表应用语义：华东组「经理及以上」可见——华东组自身、销售部、集团均命中
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Up)]);

        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));
        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 102));
    }

    [Fact]
    public async Task ScopeDown_IncludesSubtree()
    {
        var (context, service) = Create();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(1, OrgGrantScope.Down)]);

        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));
        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 102));
        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 103));
    }

    [Fact]
    public async Task MultiRecords_Union()
    {
        var (context, service) = Create();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Self), Grant(4, OrgGrantScope.Self)]);

        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));
        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 103));
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
    }

    [Fact]
    public async Task Replace_OverwritesAndClears()
    {
        var (context, service) = Create();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Self)]);
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(2, OrgGrantScope.Self)]);

        var grants = await service.GetGrantsAsync(OrgGrantResourceType.Wiki, 800);
        var item = Assert.Single(grants);
        Assert.Equal(2, item.OrgRoleId);
        Assert.Equal("销售部", item.RoleName);
        Assert.Equal("/1/2/", item.RolePath);

        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));

        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, []);
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
        Assert.Empty(await service.GetGrantsAsync(OrgGrantResourceType.Wiki, 800));
    }

    [Fact]
    public async Task Replace_DuplicateRole_Throws400()
    {
        var (context, service) = Create();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Self), Grant(3, OrgGrantScope.Up)]));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Replace_MissingRole_Throws400()
    {
        var (context, service) = Create();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(999, OrgGrantScope.Self)]));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task GuidResource_SameSemantics()
    {
        var (context, service) = Create();
        var appId = Guid.CreateVersion7();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.App, appId, [Grant(3, OrgGrantScope.Up)]);

        Assert.True(await service.IsAllowedAsync(OrgGrantResourceType.App, appId, 102));
        Assert.False(await service.IsAllowedAsync(OrgGrantResourceType.App, Guid.CreateVersion7(), 102));
    }

    [Fact]
    public async Task ListAllowedRoles_ExpandsByScope()
    {
        var (context, service) = Create();

        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Up)]);
        Assert.Equal([1, 2, 3], (await service.ListAllowedRolesAsync(OrgGrantResourceType.Wiki, 800)).Select(x => x.OrgRoleId).ToList());

        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(1, OrgGrantScope.Down)]);
        Assert.Equal([1, 2, 3, 4], (await service.ListAllowedRolesAsync(OrgGrantResourceType.Wiki, 800)).Select(x => x.OrgRoleId).ToList());

        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(2, OrgGrantScope.Self)]);
        var only = await service.ListAllowedRolesAsync(OrgGrantResourceType.Wiki, 800);
        var item = Assert.Single(only);
        Assert.Equal(2, item.OrgRoleId);
    }

    [Fact]
    public async Task ListAllowedUsers_UnionOfBindings()
    {
        var (context, service) = Create();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Up)]);
        Assert.Equal([100L, 101, 102, 103], (await service.ListAllowedUserIdsAsync(OrgGrantResourceType.Wiki, 800)).OrderBy(x => x).ToList());

        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(2, OrgGrantScope.Self)]);
        Assert.Equal([101L], await service.ListAllowedUserIdsAsync(OrgGrantResourceType.Wiki, 800));
    }

    [Fact]
    public async Task ListAllowedResourceIds_FiltersByUser()
    {
        var (context, service) = Create();
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Self)]);
        await service.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 801, [Grant(1, OrgGrantScope.Self)]);

        Assert.Equal(["800"], await service.ListAllowedResourceIdsAsync(OrgGrantResourceType.Wiki, 100));
        Assert.Equal(["801"], await service.ListAllowedResourceIdsAsync(OrgGrantResourceType.Wiki, 102));
        Assert.Empty(await service.ListAllowedResourceIdsAsync(OrgGrantResourceType.Wiki, 101));
    }

    [Fact]
    public async Task RoleMove_ReevaluatesByNewPath()
    {
        var cache = new InMemoryOrgGrantCache();
        var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(1, "集团", null, "/1/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(2, "销售部", 1, "/1/2/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(4, "研发部", 1, "/1/4/"));
        db.Context.Users.Add(OrgTestSeed.User(101, "u101", "用户101"));
        db.Context.Users.Add(OrgTestSeed.User(104, "u104", "用户104"));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(101, 2));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(104, 4));
        db.Context.SaveChanges();
        var moveHandler = new UpdateOrgRoleCommandHandler(db.Context, cache);

        var grantService = new OrgGrantService(db.Context, cache);
        await grantService.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Up)]);
        Assert.True(await grantService.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));

        // 华东组迁到研发部下：销售部不再是其祖先，研发部成为祖先
        await moveHandler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 3, Name = "华东组", ParentId = 4 }, CancellationToken.None);
        Assert.Equal("/1/4/3/", (await db.Context.OrgRoles.FirstAsync(x => x.Id == 3)).Path);

        Assert.False(await grantService.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 101));
        Assert.True(await grantService.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 104));
    }

    [Fact]
    public async Task DeleteRole_CascadesGrantsAndBindings()
    {
        var cache = new InMemoryOrgGrantCache();
        var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", null, "/3/"));
        db.Context.Users.Add(OrgTestSeed.User(100, "u100", "用户100"));
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 3));
        db.Context.SaveChanges();
        var grantService = new OrgGrantService(db.Context, cache);
        await grantService.ReplaceGrantsAsync(OrgGrantResourceType.Wiki, 800, [Grant(3, OrgGrantScope.Self)]);
        Assert.True(await grantService.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));

        var deleteHandler = new DeleteOrgRoleCommandHandler(db.Context, cache);
        await deleteHandler.Handle(new DeleteOrgRoleCommand { OrgRoleId = 3 }, CancellationToken.None);

        Assert.Empty(await grantService.GetGrantsAsync(OrgGrantResourceType.Wiki, 800));
        Assert.False(await grantService.IsAllowedAsync(OrgGrantResourceType.Wiki, 800, 100));
        Assert.Equal(0, await db.Context.OrgUserRoles.CountAsync(x => x.UserId == 100));
    }
}
