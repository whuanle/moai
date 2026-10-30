using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Handlers;
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

    private static async Task<SubsystemEntity> SeedSubsystemAsync(DatabaseContext context, string key = "report", string callback = "https://report.local/cb", SubsystemTeamScope scope = SubsystemTeamScope.AllTeams, SubsystemStatus status = SubsystemStatus.Enabled)
    {
        var entity = new SubsystemEntity
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Name = key,
            Description = string.Empty,
            Secret = "hash",
            SecretSalt = "salt",
            Status = status,
            TeamScope = scope,
            CallbackUrl = callback ?? string.Empty,
        };
        context.Subsystems.Add(entity);
        await context.SaveChangesAsync();
        return entity;
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
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Authorize_SpecifiedTeams_UnauthorizedTeam_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var subsystem = await SeedSubsystemAsync(db.Context, scope: SubsystemTeamScope.SpecifiedTeams);
        CreateSubsystemCommandHandlerTests.SeedTeam(db.Context, 2);
        db.Context.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity { Id = Guid.CreateVersion7(), SubsystemId = subsystem.Id, TeamId = 2 });
        await db.Context.SaveChangesAsync();
        await SeedMemberAsync(db.Context, 1, 10);
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore());

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
        var handler = new SubsystemSsoAuthorizeCommandHandler(db.Context, FakeCodeStore());

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
        var authorizeHandler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore);
        var authorized = await authorizeHandler.Handle(new SubsystemSsoAuthorizeCommand
        {
            SubsystemKey = subsystem.Key,
            ContextUserId = 10,
        }, CancellationToken.None);
        Assert.Equal("https://report.local/cb", authorized.CallbackUrl);
        Assert.NotEmpty(authorized.Code);

        var tokenProvider = new Mock<ISubsystemTokenProvider>();
        tokenProvider.Setup(x => x.GenerateUserTokens(It.IsAny<SubsystemEntity>(), It.Is<long>(id => id == 10)))
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
        var authorizeHandler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore);
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
        var authorizeHandler = new SubsystemSsoAuthorizeCommandHandler(db.Context, codeStore);
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
            RefreshToken = "bad-token",
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
        tokenProvider.Setup(x => x.TryValidateUserRefreshToken("refresh-1", out It.Ref<Guid>.IsAny, out It.Ref<long>.IsAny))
            .Callback(new TryValidateUserRefreshTokenCallback((string _, out Guid sid, out long uid) => { sid = subsystem.Id; uid = 10; }))
            .Returns(true);
        tokenProvider.Setup(x => x.GenerateUserTokens(It.IsAny<SubsystemEntity>(), 10)).Returns(("access-2", "refresh-2", 7200));
        var handler = new SubsystemSsoRefreshCommandHandler(db.Context, tokenProvider.Object);

        var response = await handler.Handle(new SubsystemSsoRefreshCommand
        {
            SubsystemId = subsystem.Id,
            RefreshToken = "refresh-1",
        }, CancellationToken.None);
        Assert.Equal("access-2", response.AccessToken);
        Assert.Equal(10, response.UserId);
        Assert.Equal(user.UserName, response.UserName);
    }

    private delegate void TryValidateUserRefreshTokenCallback(string token, out Guid subsystemId, out long userId);
}
