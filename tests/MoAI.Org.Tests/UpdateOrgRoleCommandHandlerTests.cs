using Microsoft.EntityFrameworkCore;
using MoAI.Infra.Exceptions;
using MoAI.Org.Commands;
using MoAI.Org.Handlers;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 更新组织角色单测：改名不动 path、子树整体迁移重写 path、防环.
/// </summary>
public class UpdateOrgRoleCommandHandlerTests
{
    private static void SeedTree(MoAI.Database.DatabaseContext context)
    {
        // A(1) ── B(2) ── C(3)；D(4) 独立根
        context.OrgRoles.Add(OrgTestSeed.Role(1, "集团A", null, "/1/"));
        context.OrgRoles.Add(OrgTestSeed.Role(2, "销售部", 1, "/1/2/"));
        context.OrgRoles.Add(OrgTestSeed.Role(3, "华东组", 2, "/1/2/3/"));
        context.OrgRoles.Add(OrgTestSeed.Role(4, "集团D", null, "/4/"));
        context.SaveChanges();
    }

    [Fact]
    public async Task RenameOnly_PathUnchanged()
    {
        using var db = TestSqliteContext.Create();
        SeedTree(db.Context);
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        await handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 2, Name = "市场部", ParentId = 1 }, CancellationToken.None);

        var b = await db.Context.OrgRoles.SingleAsync(x => x.Id == 2);
        var c = await db.Context.OrgRoles.SingleAsync(x => x.Id == 3);
        Assert.Equal("市场部", b.Name);
        Assert.Equal("/1/2/", b.Path);
        Assert.Equal(1, b.ParentId);
        Assert.Equal("/1/2/3/", c.Path);
    }

    [Fact]
    public async Task MoveSubtree_RewritesAllDescendantPaths()
    {
        using var db = TestSqliteContext.Create();
        SeedTree(db.Context);
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        await handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 2, Name = "销售部", ParentId = 4 }, CancellationToken.None);

        var b = await db.Context.OrgRoles.SingleAsync(x => x.Id == 2);
        var c = await db.Context.OrgRoles.SingleAsync(x => x.Id == 3);
        Assert.Equal("/4/2/", b.Path);
        Assert.Equal(4, b.ParentId);
        Assert.Equal("/4/2/3/", c.Path);
        Assert.Equal(2, c.ParentId);
    }

    [Fact]
    public async Task MoveToRoot_PathBecomesTopLevel()
    {
        using var db = TestSqliteContext.Create();
        SeedTree(db.Context);
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        await handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 2, Name = "销售部", ParentId = null }, CancellationToken.None);

        var b = await db.Context.OrgRoles.SingleAsync(x => x.Id == 2);
        var c = await db.Context.OrgRoles.SingleAsync(x => x.Id == 3);
        Assert.Equal("/2/", b.Path);
        Assert.Null(b.ParentId);
        Assert.Equal("/2/3/", c.Path);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Throws400()
    {
        using var db = TestSqliteContext.Create();
        SeedTree(db.Context);
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        // 把 A(1) 移到 C(3) 之下：C 是 A 的后代，成环
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 1, Name = "集团A", ParentId = 3 }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Move_SelfAsParent_Throws400()
    {
        using var db = TestSqliteContext.Create();
        SeedTree(db.Context);
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 2, Name = "销售部", ParentId = 2 }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Move_DuplicateNameUnderTargetParent_Throws409()
    {
        using var db = TestSqliteContext.Create();
        SeedTree(db.Context);
        db.Context.OrgRoles.Add(OrgTestSeed.Role(5, "销售部", 4, "/4/5/"));
        db.Context.SaveChanges();
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 2, Name = "销售部", ParentId = 4 }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Update_RoleMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = new UpdateOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleCommand { OrgRoleId = 999, Name = "不存在", ParentId = null }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }
}
