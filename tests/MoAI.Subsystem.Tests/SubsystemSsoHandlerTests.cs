using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Org.Services;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;
using Moq;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 委托 SSO（委托码签发/兑换/刷新）处理器测试.
/// </summary>
public class SubsystemSsoHandlerTests
{
    /// <summary>
    /// 测试用假 refresh 串（mock 固件，非真实凭据）.
    /// </summary>
    private const string FakeRefreshA = "mock-refresh" + "-A";

    /// <summary>
    /// 测试用假 refresh 串（mock 固件，非真实凭据）.
    /// </summary>
    private const string FakeRefreshBad = "mock-refresh" + "-BAD";
    /// <summary>
    /// 内存字典实现的委托码存储（一次性消费语义），供处理器测试使用.
    /// </summary>
    private static ISubsystemSsoCodeStore FakeCodeStore()
    {
        var store = new Dictionary<string, SubsystemSsoCodeStore.SsoCodePayload>();
        var mock = new Mock<ISubsystemSsoCodeStore>();
        mock.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<long>()))
            .ReturnsAsync((Guid subsystemId, long userId) =>
            {
                var code = Guid.CreateVersion7().ToString("N");
                store[code] = new SubsystemSsoCodeStore.SsoCodePayload { SubsystemId = subsystemId, UserId = userId };
                return code;
            });
        mock.Setup(x => x.ConsumeAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => store.Remove(code, out var payload) ? payload : null);
        return mock.Object;
    }

    /// <summary>
    /// 伪造统一组织授权判定服务（allowed=是否放通）.
    /// </summary>
    private static IOrgGrantService FakeOrgGrants(bool allowed = false)
    {
        var mock = new Mock<IOrgGrantService>();
        mock.Setup(x => x.IsAllowedAsync(It.IsAny<OrgGrantResourceType>(), It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowed);
        mock.Setup(x => x.IsAllowedAsync(It.IsAny<OrgGrantResourceType>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowed);
        return mock.Object;
    }

    private static Task<AccessAppEntity> SeedSubsystemAsync(DatabaseContext context, string key = "report", string callback = "https://report.local/cb", SubsystemTeamScope scope = SubsystemTeamScope.AllTeams, AccessAppStatus status = AccessAppStatus.Enabled)
    {
        return TestSeeds.SeedSubsysAppAsync(context, key, AccessAppType.Subsystem, scope, status, callback);
    }

    private static async Task<AccessAppEntity> SeedMultiCallbackSubsystemAsync(DatabaseContext context, string key, params string[] callbacks)
    {
        var entity = await TestSeeds.SeedSubsysAppAsync(context, key, callback: callbacks.Length > 0 ? callbacks[0] : string.Empty);
        entity.CallbackUrls = MoAI.Subsystem.Services.SubsystemCallbacks.Serialize(callbacks);
        await context.SaveChangesAsync();
        return entity;
    }

    [Fact]
    public async Task Authorize_RedirectUriInWhitelist_ReturnsThatUri()
    {
        using var db = TestSqliteContext.Create();
        const string prod = "https://report.local/cb";
        const string test = "https://test.report.local/cb";
        var subsystem = await SeedMultiCallbackSubsystemAsync(db.Context, "report", prod, test);
        await SeedUserAsync(db.Context, 10);
        await SeedMemberAsync(db.Context, 1, 10);

        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());
        var authorized = await handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
            RedirectUri = test,
        }, CancellationToken.None);
        Assert.Equal(test, authorized.CallbackUrl);
    }

    [Fact]
    public async Task Authorize_RedirectUriNotRegistered_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedMultiCallbackSubsystemAsync(db.Context, "report", "https://report.local/cb");
        await SeedUserAsync(db.Context, 10);
        await SeedMemberAsync(db.Context, 1, 10);

        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
            RedirectUri = "https://evil.local/cb",
        }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Authorize_RedirectUriEmpty_DefaultsToFirst()
    {
        using var db = TestSqliteContext.Create();
        const string prod = "https://report.local/cb";
        var subsystem = await SeedMultiCallbackSubsystemAsync(db.Context, "report", prod, "https://test.report.local/cb");
        await SeedUserAsync(db.Context, 10);
        await SeedMemberAsync(db.Context, 1, 10);

        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());
        var authorized = await handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None);
        Assert.Equal(prod, authorized.CallbackUrl);
    }

    private static async Task<UserEntity> SeedUserAsync(DatabaseContext context, long userId)
    {
        var user = new UserEntity
        {
            Id = userId,
            UserName = $"u{userId}",
            Email = $"{userId}@t.local",
            Password = "p",
            PasswordSalt = "s",
            NickName = $"用户{userId}",
            AvatarPath = string.Empty,
            Phone = string.Empty,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task SeedMemberAsync(DatabaseContext context, int teamId, long userId)
    {
        context.TeamUsers.Add(new TeamUserEntity { TeamId = teamId, UserId = userId });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Authorize_NoCallback_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context, callback: "");
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Authorize_AppRow_Throws404()
    {
        using var db = TestSqliteContext.Create();
        // 应用接入行即使配置了回调也不参与委托登录（appType 过滤）
        await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-AppRow", AccessAppType.App, callback: "https://erp.local/cb");
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = "moai-sa-AppRow",
            ContextUserId = 10,
        }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Authorize_SpecifiedTeams_UnauthorizedTeam_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context, scope: SubsystemTeamScope.SpecifiedTeams);
        TestSeeds.SeedTeam(db.Context, 2);
        db.Context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = subsystem.Id, TeamId = 2 });
        await db.Context.SaveChangesAsync();
        await SeedMemberAsync(db.Context, 1, 10);
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Authorize_AllTeams_WithoutAnyTeam_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Authorize_ThenExchange_ReturnsUserTokensAndProfile()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);
        await SeedUserAsync(db.Context, 10);
        await SeedMemberAsync(db.Context, 1, 10);

        var codeStore = FakeCodeStore();
        var authorizeHandler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore, FakeOrgGrants());
        var authorized = await authorizeHandler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None);
        Assert.Equal("https://report.local/cb", authorized.CallbackUrl);
        Assert.NotEmpty(authorized.Code);

        var tokenProvider = new Mock<ISubsystemTokenProvider>();
        tokenProvider.Setup(x => x.GenerateUserTokens(It.IsAny<AccessAppEntity>(), It.Is<long>(id => id == 10)))
            .Returns(("access-1", "refresh-1", 7200));
        var exchangeHandler = new SubsystemSsoExchangeCommandHandler(db.Context, codeStore, tokenProvider.Object);
        var exchanged = await exchangeHandler.Handle(new SubsystemSsoExchangeCommand
        {
            SubsystemId = subsystem.Id,
            Code = authorized.Code,
        }, CancellationToken.None);

        Assert.Equal("access-1", exchanged.AccessToken);
        Assert.Equal("refresh-1", exchanged.RefreshToken);
        Assert.Equal(10, exchanged.UserId);
        Assert.Equal("u10", exchanged.UserName);

        // 委托码一次性：重放 401
        var replay = await Assert.ThrowsAsync<BusinessException>(() => exchangeHandler.Handle(new SubsystemSsoExchangeCommand
        {
            SubsystemId = subsystem.Id,
            Code = authorized.Code,
        }, CancellationToken.None));
        Assert.Equal(401, replay.StatusCode);
    }

    [Fact]
    public async Task Exchange_CodeBoundToOtherSubsystem_Throws401()
    {
        using var db = TestSqliteContext.Create();
        await SeedSubsystemAsync(db.Context, key: "a");
        var subsystemB = await SeedSubsystemAsync(db.Context, key: "b");
        await SeedUserAsync(db.Context, 10);
        await SeedMemberAsync(db.Context, 1, 10);

        var codeStore = FakeCodeStore();
        var authorizeHandler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore, FakeOrgGrants());
        var authorized = await authorizeHandler.Handle(new SubsystemSsoAuthorizeCommand { SubsystemKey = "a", ContextUserId = 10 }, CancellationToken.None);

        // 子系统 B 拿 A 签发的委托码兑换：绑定校验拒绝
        var exchangeHandler = new SubsystemSsoExchangeCommandHandler(db.Context, codeStore, Mock.Of<ISubsystemTokenProvider>());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => exchangeHandler.Handle(new SubsystemSsoExchangeCommand
        {
            SubsystemId = subsystemB.Id,
            Code = authorized.Code,
        }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Exchange_DisabledUser_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);
        var user = await SeedUserAsync(db.Context, 10);
        user.IsDisable = true;
        await db.Context.SaveChangesAsync();
        await SeedMemberAsync(db.Context, 1, 10);

        var codeStore = FakeCodeStore();
        var authorizeHandler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore, FakeOrgGrants());
        var authorized = await authorizeHandler.Handle(new SubsystemSsoAuthorizeCommand { SubsystemKey = subsystem.Key, ContextUserId = 10 }, CancellationToken.None);

        var exchangeHandler = new SubsystemSsoExchangeCommandHandler(db.Context, codeStore, Mock.Of<ISubsystemTokenProvider>());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => exchangeHandler.Handle(new SubsystemSsoExchangeCommand
        {
            SubsystemId = subsystem.Id,
            Code = authorized.Code,
        }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Throws401()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);
        var tokenProvider = new Mock<ISubsystemTokenProvider>();
        tokenProvider.Setup(x => x.TryValidateUserRefreshToken(It.IsAny<string>(), out It.Ref<Guid>.IsAny, out It.Ref<long>.IsAny)).Returns(false);
        var handler = new SubsystemSsoRefreshCommandHandler(db.Context, tokenProvider.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoRefreshCommand
        {
            SubsystemId = subsystem.Id,
            RefreshToken = FakeRefreshBad,
        }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Refresh_Valid_ReturnsNewTokens()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);
        var user = await SeedUserAsync(db.Context, 10);

        var tokenProvider = new Mock<ISubsystemTokenProvider>();
        tokenProvider.Setup(x => x.TryValidateUserRefreshToken(FakeRefreshA, out It.Ref<Guid>.IsAny, out It.Ref<long>.IsAny))
            .Callback(new TryValidateUserRefreshTokenCallback((string _, out Guid sid, out long uid) => { sid = subsystem.Id; uid = 10; }))
            .Returns(true);
        tokenProvider.Setup(x => x.GenerateUserTokens(It.IsAny<AccessAppEntity>(), 10)).Returns(("access-2", "refresh-2", 7200));
        var handler = new SubsystemSsoRefreshCommandHandler(db.Context, tokenProvider.Object);

        var response = await handler.Handle(new SubsystemSsoRefreshCommand
        {
            SubsystemId = subsystem.Id,
            RefreshToken = FakeRefreshA,
        }, CancellationToken.None);
        Assert.Equal("access-2", response.AccessToken);
        Assert.Equal(10, response.UserId);
        Assert.Equal(user.UserName, response.UserName);
    }

    private delegate void TryValidateUserRefreshTokenCallback(string token, out Guid subsystemId, out long userId);

    [Fact]
    public async Task Authorize_SpecifiedTeams_TeamMiss_ButOrgGrantHit_Passes()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context, scope: SubsystemTeamScope.SpecifiedTeams);
        TestSeeds.SeedTeam(db.Context, 2);
        db.Context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = subsystem.Id, TeamId = 2 });
        await db.Context.SaveChangesAsync();
        // 用户只在未授权的团队 1，但组织维度放通：并集命中
        await SeedMemberAsync(db.Context, 1, 10);

        var codeStore = FakeCodeStore();
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore, FakeOrgGrants(allowed: true));
        var authorized = await handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None);
        Assert.NotEmpty(authorized.Code);
    }

    [Fact]
    public async Task Authorize_AllTeams_NoTeam_ButOrgGrantHit_Passes()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);

        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore(), FakeOrgGrants(allowed: true));
        var authorized = await handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None);
        Assert.NotEmpty(authorized.Code);
    }

    [Fact]
    public async Task SetOrgGrants_SubsystemMissing_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var handler = new SetSubsystemOrgGrantsCommandHandler(db.Context, Mock.Of<IOrgGrantService>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SetSubsystemOrgGrantsCommand
        {
            SubsystemId = Guid.CreateVersion7(),
            Grants = [new SubsystemOrgGrantItem { OrgRoleId = 1, Scope = OrgGrantScope.Down }],
        }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task SetOrgGrants_DelegatesToOrgGrantService_WithMappedItems()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);

        OrgGrantResourceType? capturedType = null;
        Guid capturedId = default;
        IReadOnlyList<OrgGrantItem>? capturedGrants = null;
        var service = new Mock<IOrgGrantService>();
        service.Setup(x => x.ReplaceGrantsAsync(It.IsAny<OrgGrantResourceType>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<OrgGrantItem>>(), It.IsAny<CancellationToken>()))
            .Callback((OrgGrantResourceType type, Guid id, IReadOnlyList<OrgGrantItem> grants, CancellationToken _) =>
            {
                capturedType = type;
                capturedId = id;
                capturedGrants = grants;
            })
            .Returns(Task.CompletedTask);

        var handler = new SetSubsystemOrgGrantsCommandHandler(db.Context, service.Object);
        await handler.Handle(new SetSubsystemOrgGrantsCommand
        {
            SubsystemId = subsystem.Id,
            Grants = [new SubsystemOrgGrantItem { OrgRoleId = 3, Scope = OrgGrantScope.Up }],
        }, CancellationToken.None);

        Assert.Equal(OrgGrantResourceType.Subsystem, capturedType);
        Assert.Equal(subsystem.Id, capturedId);
        Assert.NotNull(capturedGrants);
        var item = Assert.Single(capturedGrants!);
        Assert.Equal(3, item.OrgRoleId);
        Assert.Equal(OrgGrantScope.Up, item.Scope);
    }

    [Fact]
    public async Task QueryOrgGrants_MapsServiceGrants()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context);

        var service = new Mock<IOrgGrantService>();
        service.Setup(x => x.GetGrantsAsync(OrgGrantResourceType.Subsystem, subsystem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OrgGrantInfo { OrgRoleId = 3, RoleName = "经理", RolePath = "/1/3/", Scope = OrgGrantScope.Self }]);

        var handler = new QuerySubsystemOrgGrantsCommandHandler(db.Context, service.Object);
        var response = await handler.Handle(new QuerySubsystemOrgGrantsCommand { SubsystemId = subsystem.Id }, CancellationToken.None);

        var item = Assert.Single(response.Items);
        Assert.Equal(3, item.OrgRoleId);
        Assert.Equal("经理", item.RoleName);
        Assert.Equal("/1/3/", item.RolePath);
        Assert.Equal(OrgGrantScope.Self, item.Scope);
    }

    [Fact]
    public async Task QueryOrgGrants_AppRow_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var app = await TestSeeds.SeedSubsysAppAsync(db.Context, "moai-sa-AppRow", AccessAppType.App);
        var handler = new QuerySubsystemOrgGrantsCommandHandler(db.Context, Mock.Of<IOrgGrantService>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new QuerySubsystemOrgGrantsCommand { SubsystemId = app.Id }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }
}
