using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Services;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 系统应用接入：scope 解析 + CRUD 处理器测试.
/// </summary>
public class SystemAccessAppTests
{
    [Fact]
    public void TryParseCodes_ValidCodes_CombinesFlags()
    {
        var cases = new (string[] Codes, SystemApiKeyScopes Expected)[]
        {
            (["org_read"], SystemApiKeyScopes.OrgRead),
            (["org_read", "org_write"], SystemApiKeyScopes.OrgRead | SystemApiKeyScopes.OrgWrite),
            (["org_read", "org_write", "user_read", "user_write"], SystemApiKeyScopes.OrgRead | SystemApiKeyScopes.OrgWrite | SystemApiKeyScopes.UserRead | SystemApiKeyScopes.UserWrite),
            (["team_read", "team_write"], SystemApiKeyScopes.TeamRead | SystemApiKeyScopes.TeamWrite),
        };
        foreach (var (codes, expected) in cases)
        {
            Assert.True(SystemApiKeyScopeCodes.TryParseCodes(codes, out var scopes), string.Join(',', codes));
            Assert.Equal(expected, scopes);
        }
    }

    [Fact]
    public void TryParseCodes_InvalidCodes_ReturnsFalse()
    {
        Assert.False(SystemApiKeyScopeCodes.TryParseCodes([], out _));
        Assert.False(SystemApiKeyScopeCodes.TryParseCodes(["unknown_scope"], out _));
        Assert.False(SystemApiKeyScopeCodes.TryParseCodes(["org_read", "org_read"], out _));
    }

    [Fact]
    public void ToCodes_RoundTrips()
    {
        var scopes = SystemApiKeyScopes.OrgWrite | SystemApiKeyScopes.UserRead;
        var codes = SystemApiKeyScopeCodes.ToCodes(scopes);
        Assert.Equal(["org_write", "user_read"], codes.Order().ToList());
        Assert.True(SystemApiKeyScopeCodes.TryParseCodes(codes, out var parsed));
        Assert.Equal(scopes, parsed);
    }

    [Fact]
    public async Task Create_ReturnsKeyOnce_AndStoresPrefix()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSystemAccessAppCommandHandler(db.Context);

        var response = await handler.Handle(new CreateSystemAccessAppCommand
        {
            Name = "ERP 组织同步",
            Description = "同步 ERP 组织角色",
            Scopes = ["org_read", "org_write"],
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.StartsWith("moai-sa-", response.Key);
        Assert.Equal(40, response.Key.Length);
        Assert.Equal("moai-sa-", response.KeyPrefix[..8]);
        Assert.Equal(48, response.Secret.Length);

        var entity = await db.Context.AccessApps.FindAsync(response.Id);
        Assert.NotNull(entity);
        // appid 明文存储（Bearer 直连凭证），密钥只存 PBKDF2 哈希，列表仅回显前缀
        Assert.Equal(response.Key, entity!.Key);
        Assert.False(string.IsNullOrEmpty(entity.Secret));
        Assert.False(string.IsNullOrEmpty(entity.SecretSalt));
        Assert.Equal(AccessAppLevel.System, entity.Level);
        Assert.Equal(0, entity.TeamId);
        Assert.Equal((int)(SystemApiKeyScopes.OrgRead | SystemApiKeyScopes.OrgWrite), entity.Scopes);
        Assert.Equal(AccessAppStatus.Enabled, entity.Status);
    }

    [Fact]
    public async Task Create_InvalidScopes_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var handler = new CreateSystemAccessAppCommandHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new CreateSystemAccessAppCommand
        {
            Name = "x",
            Scopes = ["not_a_scope"],
        }, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesScopesAndStatus_MissingThrows404()
    {
        using var db = TestSqliteContext.Create();
        var create = new CreateSystemAccessAppCommandHandler(db.Context);
        var created = await create.Handle(new CreateSystemAccessAppCommand { Name = "a", Scopes = ["user_read"] }, CancellationToken.None);

        var update = new UpdateSystemAccessAppCommandHandler(db.Context);
        await update.Handle(new UpdateSystemAccessAppCommand
        {
            Id = created.Id,
            Name = "b",
            Scopes = ["user_read", "user_write"],
            Status = AccessAppStatus.Disabled,
        }, CancellationToken.None);

        var entity = await db.Context.AccessApps.FindAsync(created.Id);
        Assert.Equal("b", entity!.Name);
        Assert.Equal((int)(SystemApiKeyScopes.UserRead | SystemApiKeyScopes.UserWrite), entity.Scopes);
        Assert.Equal(AccessAppStatus.Disabled, entity.Status);

        var missing = await Assert.ThrowsAsync<BusinessException>(() => update.Handle(new UpdateSystemAccessAppCommand
        {
            Id = Guid.NewGuid(),
            Name = "x",
            Scopes = ["org_read"],
            Status = AccessAppStatus.Enabled,
        }, CancellationToken.None));
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task Delete_SoftDeletes_KeyNoLongerVisible()
    {
        using var db = TestSqliteContext.Create();
        var create = new CreateSystemAccessAppCommandHandler(db.Context);
        var created = await create.Handle(new CreateSystemAccessAppCommand { Name = "a", Scopes = ["org_read"] }, CancellationToken.None);

        var delete = new DeleteSystemAccessAppCommandHandler(db.Context);
        await delete.Handle(new DeleteSystemAccessAppCommand { Id = created.Id }, CancellationToken.None);

        // 对照：SubsystemEntity 同样软删后查询（验证全局过滤器在该上下文是否整体生效）
        var subsystem = new SubsystemEntity { Key = "k1", Name = "n", Description = string.Empty, Secret = "s", SecretSalt = "t", Status = SubsystemStatus.Enabled, TeamScope = SubsystemTeamScope.AllTeams, CallbackUrl = string.Empty };
        db.Context.Subsystems.Add(subsystem);
        await db.Context.SaveChangesAsync(CancellationToken.None);
        db.Context.Subsystems.Remove(subsystem);
        await db.Context.SaveChangesAsync(CancellationToken.None);
        var subsystemAfter = await db.Context.Subsystems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == subsystem.Id);

        // 软删后走数据库查询（避开 FindAsync 的跟踪缓存）应不可见
        Assert.Null(subsystemAfter);

        // 应用接入为软删除：全局过滤器屏蔽后查询不可见（key 立即失效）
        Assert.Null(await db.Context.AccessApps.AsNoTracking().FirstOrDefaultAsync(x => x.Id == created.Id));
    }

    [Fact]
    public async Task Query_ListsScopesCodes_AndFiltersByName()
    {
        using var db = TestSqliteContext.Create();
        var create = new CreateSystemAccessAppCommandHandler(db.Context);
        await create.Handle(new CreateSystemAccessAppCommand { Name = "ERP 同步", Scopes = ["org_read", "org_write"] }, CancellationToken.None);
        await create.Handle(new CreateSystemAccessAppCommand { Name = "门户", Scopes = ["user_read"] }, CancellationToken.None);

        var fill = Moq.Mock.Of<MoAI.Account.Services.IUserInfoFillService>();
        var handler = new QuerySystemAccessAppsCommandHandler(db.Context, fill);

        var all = await handler.Handle(new QuerySystemAccessAppsCommand(), CancellationToken.None);
        Assert.Equal(2, all.TotalCount);

        var filtered = await handler.Handle(new QuerySystemAccessAppsCommand { Search = "门户" }, CancellationToken.None);
        var item = Assert.Single(filtered.Items);
        Assert.Equal(["user_read"], item.Scopes);
        Assert.Equal(AccessAppStatus.Enabled, item.Status);
    }
}
