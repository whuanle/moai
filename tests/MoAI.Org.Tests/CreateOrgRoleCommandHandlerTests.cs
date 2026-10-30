using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Org.Commands;
using MoAI.Org.Handlers;
using Xunit;

namespace MoAI.Org.Tests;

/// <summary>
/// 创建组织角色单测：path 计算 / 父校验 / 同父重名.
/// </summary>
public class CreateOrgRoleCommandHandlerTests
{
    [Fact]
    public async Task CreateRootRole_PathIsSlashIdSlash()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateOrgRoleCommandHandler(db.Context);

        var result = await handler.Handle(new CreateOrgRoleCommand { Name = "集团总部" }, CancellationToken.None);

        Assert.Equal($"/{result.OrgRoleId}/", result.Path);
        var entity = await db.Context.OrgRoles.SingleAsync(x => x.Id == result.OrgRoleId);
        Assert.Null(entity.ParentId);
        Assert.Equal(result.Path, entity.Path);
    }

    [Fact]
    public async Task CreateChildRole_AppendsParentPath()
    {
        using var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(10, "集团总部", null, "/10/"));
        await db.Context.SaveChangesAsync();
        var handler = new CreateOrgRoleCommandHandler(db.Context);

        var result = await handler.Handle(new CreateOrgRoleCommand { Name = "销售部", ParentId = 10 }, CancellationToken.None);

        Assert.Equal($"/10/{result.OrgRoleId}/", result.Path);
        var entity = await db.Context.OrgRoles.SingleAsync(x => x.Id == result.OrgRoleId);
        Assert.Equal(10, entity.ParentId);
    }

    [Fact]
    public async Task CreateDeepChain_PathChainsEachLevel()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateOrgRoleCommandHandler(db.Context);
        var root = await handler.Handle(new CreateOrgRoleCommand { Name = "L1" }, CancellationToken.None);
        var child = await handler.Handle(new CreateOrgRoleCommand { Name = "L2", ParentId = root.OrgRoleId }, CancellationToken.None);
        var grandchild = await handler.Handle(new CreateOrgRoleCommand { Name = "L3", ParentId = child.OrgRoleId }, CancellationToken.None);

        Assert.Equal($"/{root.OrgRoleId}/", root.Path);
        Assert.Equal($"/{root.OrgRoleId}/{child.OrgRoleId}/", child.Path);
        Assert.Equal($"/{root.OrgRoleId}/{child.OrgRoleId}/{grandchild.OrgRoleId}/", grandchild.Path);
    }

    [Fact]
    public async Task Create_ParentNotFound_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new CreateOrgRoleCommand { Name = "销售部", ParentId = 999 }, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameUnderSameParent_Throws409()
    {
        using var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(10, "集团总部", null, "/10/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(11, "销售部", 10, "/10/11/"));
        await db.Context.SaveChangesAsync();
        var handler = new CreateOrgRoleCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.Handle(new CreateOrgRoleCommand { Name = "销售部", ParentId = 10 }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Create_SameNameUnderDifferentParent_Allowed()
    {
        using var db = TestSqliteContext.Create();
        db.Context.OrgRoles.Add(OrgTestSeed.Role(10, "华东集团", null, "/10/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(20, "华北集团", null, "/20/"));
        db.Context.OrgRoles.Add(OrgTestSeed.Role(11, "销售部", 10, "/10/11/"));
        await db.Context.SaveChangesAsync();
        var handler = new CreateOrgRoleCommandHandler(db.Context);

        var result = await handler.Handle(new CreateOrgRoleCommand { Name = "销售部", ParentId = 20 }, CancellationToken.None);

        Assert.Equal($"/20/{result.OrgRoleId}/", result.Path);
    }

    [Fact]
    public void Validate_NameEmpty_Fails()
    {
        var validator = new InlineValidator<CreateOrgRoleCommand>();
        CreateOrgRoleCommand.Validate(validator);

        var result = validator.Validate(new CreateOrgRoleCommand { Name = string.Empty });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ParentIdNotPositive_Fails()
    {
        var validator = new InlineValidator<CreateOrgRoleCommand>();
        CreateOrgRoleCommand.Validate(validator);

        var result = validator.Validate(new CreateOrgRoleCommand { Name = "销售部", ParentId = 0 });

        Assert.False(result.IsValid);
    }
}
