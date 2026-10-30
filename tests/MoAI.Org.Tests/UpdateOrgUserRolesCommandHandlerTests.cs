using Microsoft.EntityFrameworkCore;
using MoAI.Infra.Exceptions;
using MoAI.Org.Commands;
using MoAI.Org.Handlers;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 用户组织角色分配单测：整体覆盖、去重、用户/角色存在性.
/// </summary>
public class UpdateOrgUserRolesCommandHandlerTests
{
    private static void Seed(MoAI.Database.DatabaseContext context)
    {
        context.Users.Add(OrgTestSeed.User(100, "zhangsan", "张三"));
        context.OrgRoles.Add(OrgTestSeed.Role(1, "集团A", null, "/1/"));
        context.OrgRoles.Add(OrgTestSeed.Role(2, "销售部", 1, "/1/2/"));
        context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        context.SaveChanges();
    }

    [Fact]
    public async Task Assign_ThenOverride_ReplacesWholeSet()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgUserRolesCommandHandler(db.Context);

        await handler.Handle(new UpdateOrgUserRolesCommand { UserId = 100, RoleIds = [1, 2] }, CancellationToken.None);
        var first = await db.Context.OrgUserRoles.Where(x => x.UserId == 100).Select(x => x.RoleId).OrderBy(x => x).ToListAsync();
        Assert.Equal([1, 2], first);

        await handler.Handle(new UpdateOrgUserRolesCommand { UserId = 100, RoleIds = [2, 3] }, CancellationToken.None);
        var second = await db.Context.OrgUserRoles.Where(x => x.UserId == 100).Select(x => x.RoleId).OrderBy(x => x).ToListAsync();
        Assert.Equal([2, 3], second);
    }

    [Fact]
    public async Task Assign_EmptyList_ClearsAll()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        db.Context.OrgUserRoles.Add(OrgTestSeed.Binding(100, 1));
        await db.Context.SaveChangesAsync();
        var handler = new UpdateOrgUserRolesCommandHandler(db.Context);

        await handler.Handle(new UpdateOrgUserRolesCommand { UserId = 100, RoleIds = [] }, CancellationToken.None);

        Assert.Equal(0, await db.Context.OrgUserRoles.CountAsync(x => x.UserId == 100));
    }

    [Fact]
    public async Task Assign_DeduplicatesInput()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgUserRolesCommandHandler(db.Context);

        await handler.Handle(new UpdateOrgUserRolesCommand { UserId = 100, RoleIds = [1, 1, 2] }, CancellationToken.None);

        Assert.Equal(2, await db.Context.OrgUserRoles.CountAsync(x => x.UserId == 100));
    }

    [Fact]
    public async Task Assign_UserMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgUserRolesCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgUserRolesCommand { UserId = 999, RoleIds = [1] }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Assign_RoleMissing_Throws400()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgUserRolesCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgUserRolesCommand { UserId = 100, RoleIds = [1, 999] }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }
}
