using Microsoft.EntityFrameworkCore;
using MoAI.Infra.Exceptions;
using MoAI.Org.Commands;
using MoAI.Org.Handlers;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 组织排序单测：同级置顶/插位重编号、插入参照校验、跨级移动带子树 path 重写与落位.
/// </summary>
public class UpdateOrgRoleOrderCommandHandlerTests
{
    private static void Seed(MoAI.Database.DatabaseContext context)
    {
        // 根：1(sort1) 10(sort2)；1 的子：2(sort1) 12(sort2)；10 的子：11(sort1)
        context.OrgRoles.Add(Role(1, "集团A", null, "/1/", 1));
        context.OrgRoles.Add(Role(10, "集团D", null, "/10/", 2));
        context.OrgRoles.Add(Role(2, "销售部", 1, "/1/2/", 1));
        context.OrgRoles.Add(Role(12, "西南组", 1, "/1/12/", 2));
        context.OrgRoles.Add(Role(11, "市场部", 10, "/10/11/", 1));
        context.SaveChanges();
    }

    private static MoAI.Database.Entities.OrgRoleEntity Role(int id, string name, int? parentId, string path, int sortOrder)
    {
        var entity = OrgTestSeed.Role(id, name, parentId, path);
        entity.SortOrder = sortOrder;
        return entity;
    }

    [Fact]
    public async Task ReorderToFirst_RenumbersSiblings()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        await handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 10, ParentId = null, PrevOrgRoleId = null }, CancellationToken.None);

        Assert.Equal(1, db.Context.OrgRoles.Single(x => x.Id == 10).SortOrder);
        Assert.Equal(2, db.Context.OrgRoles.Single(x => x.Id == 1).SortOrder);
    }

    [Fact]
    public async Task InsertAfterSibling_LandsRightAfterTarget()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        // 10 插到 1 之后：1 → 10 → 原其余
        await handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 10, ParentId = null, PrevOrgRoleId = 1 }, CancellationToken.None);

        Assert.Equal(1, db.Context.OrgRoles.Single(x => x.Id == 1).SortOrder);
        Assert.Equal(2, db.Context.OrgRoles.Single(x => x.Id == 10).SortOrder);
    }

    [Fact]
    public async Task PrevNotSibling_Throws400()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        // 11 是 10 的子组织，不是根层同级
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 1, ParentId = null, PrevOrgRoleId = 11 }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SelfAsPrev_Throws400()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 1, ParentId = null, PrevOrgRoleId = 1 }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task MoveAcrossLevel_RewritesSubtreeAndLandsAtPosition()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        // 10（子树含 11）移到根层、置于 1 之后：path 重写为 /10/ 与 /10/11/
        await handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 10, ParentId = null, PrevOrgRoleId = 1 }, CancellationToken.None);

        var moved = db.Context.OrgRoles.Single(x => x.Id == 10);
        Assert.Null(moved.ParentId);
        Assert.Equal("/10/", moved.Path);
        Assert.Equal("/10/11/", db.Context.OrgRoles.Single(x => x.Id == 11).Path);
        Assert.Equal(2, moved.SortOrder);
        Assert.Equal(1, db.Context.OrgRoles.Single(x => x.Id == 1).SortOrder);
    }

    [Fact]
    public async Task MoveIntoOwnDescendant_Throws400()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 1, ParentId = 2, PrevOrgRoleId = null }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task RoleMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        Seed(db.Context);
        var handler = new UpdateOrgRoleOrderCommandHandler(db.Context, new InMemoryOrgGrantCache());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new UpdateOrgRoleOrderCommand { OrgRoleId = 999, ParentId = null, PrevOrgRoleId = null }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }
}
