using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
/// 系统级接入 token 换取处理器测试：子系统与应用接入同一套换取（v2 可逆格式与存量哈希两种存储均可校验）.
/// </summary>
public class SubsystemTokenCommandHandlerTests
{
    private const string TestSecret = "unit-test-secret-000000000000000000000000";

    private static SubsystemTokenCommandHandler CreateHandler(MoAI.Database.DatabaseContext context)
    {
        var tokenProvider = new Mock<ISubsystemTokenProvider>();
        tokenProvider.Setup(x => x.GenerateMachineTokens(It.IsAny<MoAI.Database.Entities.AccessAppEntity>())).Returns(("token", "refresh", 7200));
        var rateLimiter = new Mock<ISubsysTokenRateLimiter>();
        rateLimiter.Setup(x => x.IsBlocked(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        return new SubsystemTokenCommandHandler(context, tokenProvider.Object, TestSeeds.CreateCipher(), rateLimiter.Object);
    }

    [Fact]
    public async Task Handle_SubsystemCorrectSecret_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "report", status: AccessAppStatus.Enabled, callback: string.Empty);
        await SetSecretAsync(db, "report", TestSecret);
        var handler = CreateHandler(db.Context);

        var response = await handler.Handle(new SubsystemTokenCommand { AppId = "report", Secret = TestSecret }, CancellationToken.None);
        Assert.Equal("Bearer", response.TokenType);
        Assert.True(response.ExpiresIn > 0);
    }

    [Fact]
    public async Task Handle_LegacyPbkdf2StoredSecret_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        // 存量行：无 v2: 前缀的 PBKDF2 哈希 + 盐，验证仍走哈希
        var (hash, salt) = MoAI.Infra.Helpers.PBKDF2Helper.ToHash(TestSecret);
        await TestSeeds.SeedSubsysAppAsync(db.Context, "legacy", secret: hash, secretMask: string.Empty);
        var entity = await db.Context.AccessApps.FirstAsync(x => x.Key == "legacy");
        entity.SecretSalt = salt;
        await db.Context.SaveChangesAsync();

        var handler = CreateHandler(db.Context);
        var response = await handler.Handle(new SubsystemTokenCommand { AppId = "legacy", Secret = TestSecret }, CancellationToken.None);
        Assert.Equal("Bearer", response.TokenType);
    }

    [Fact]
    public async Task Handle_AppMoaiSaKey_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        const string key = "moai-sa-AbCdEf12345678901234567890123456";
        await TestSeeds.SeedSubsysAppAsync(db.Context, key, MoAI.Database.Enums.AccessAppType.App);
        await SetSecretAsync(db, key, TestSecret);
        var handler = CreateHandler(db.Context);

        var response = await handler.Handle(new SubsystemTokenCommand { AppId = key, Secret = TestSecret }, CancellationToken.None);
        Assert.Equal("Bearer", response.TokenType);
    }

    [Fact]
    public async Task Handle_WrongSecret_Throws401()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "report");
        await SetSecretAsync(db, "report", TestSecret);
        var handler = CreateHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { AppId = "report", Secret = "wrong" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_UnknownKey_Throws401()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "report");
        await SetSecretAsync(db, "report", TestSecret);
        var handler = CreateHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { AppId = "nope", Secret = TestSecret }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_Disabled_Throws403()
    {
        using var db = TestSqliteContext.Create();
        await TestSeeds.SeedSubsysAppAsync(db.Context, "report", status: AccessAppStatus.Disabled);
        await SetSecretAsync(db, "report", TestSecret);
        var handler = CreateHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { AppId = "report", Secret = TestSecret }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_ResetSecret_OldSecretRejected()
    {
        using var db = TestSqliteContext.Create();
        var entity = await TestSeeds.SeedSubsysAppAsync(db.Context, "report");
        await SetSecretAsync(db, "report", TestSecret);
        var resetHandler = new ResetSubsysAppSecretCommandHandler(db.Context, TestSeeds.CreateCipher(), Mock.Of<ISubsysTokenRateLimiter>());
        await resetHandler.Handle(new ResetSubsysAppSecretCommand { Id = entity.Id }, CancellationToken.None);

        // 旧密钥失效 401，新密钥（重置后从库内回显）可用
        var cipher = TestSeeds.CreateCipher();
        var updated = await db.Context.AccessApps.FirstAsync(x => x.Key == "report");
        Assert.True(cipher.TryReveal(updated.Secret, out var newSecret));

        var handler = CreateHandler(db.Context);
        var oldEx = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { AppId = "report", Secret = TestSecret }, CancellationToken.None));
        Assert.Equal(401, oldEx.StatusCode);

        await handler.Handle(new SubsystemTokenCommand { AppId = "report", Secret = newSecret }, CancellationToken.None);
    }

    /// <summary>
    /// 种子行 Secret 固定占位：改写为指定密钥的 v2 可逆密文.
    /// </summary>
    private static async Task SetSecretAsync(TestSqliteContext db, string key, string secret)
    {
        var cipher = TestSeeds.CreateCipher();
        var entity = await db.Context.AccessApps.FirstAsync(x => x.Key == key);
        entity.Secret = cipher.EncryptForStorage(secret);
        entity.SecretSalt = string.Empty;
        entity.SecretMask = cipher.Mask(secret);
        await db.Context.SaveChangesAsync();
    }
}
