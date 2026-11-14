using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Services;
using Moq;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 密钥回显（RSA+AES 信封）处理器测试：v2 可回显 / 存量哈希 400 / 信封校验.
/// </summary>
public class RevealSubsysAppSecretCommandHandlerTests
{
    /// <summary>
    /// 伪造 RSA 服务：Decrypt 直接返回固定信封 key 的 base64（免真实 RSA，加密链路由 E2E 覆盖）.
    /// </summary>
    private static (IRsaProvider Rsa, byte[] Key) FakeRsa()
    {
        var key = RandomNumberGenerator.GetBytes(SubsystemSecretEnvelope.KeySizeBytes);
        var rsa = new Mock<IRsaProvider>();
        rsa.Setup(x => x.Decrypt(It.IsAny<string>(), null)).Returns(Convert.ToBase64String(key));
        return (rsa.Object, key);
    }

    private static async Task<string> SeedReversibleAsync(TestSqliteContext db)
    {
        var cipher = TestSeeds.CreateCipher();
        var secret = cipher.NewSecret();
        var entity = await TestSeeds.SeedSubsysAppAsync(db.Context, "report", secret: cipher.EncryptForStorage(secret), secretMask: cipher.Mask(secret));
        entity.SecretSalt = string.Empty;
        await db.Context.SaveChangesAsync();
        // 回读 id 与原文对齐：掩码来自原文，断言用
        return secret;
    }

    [Fact]
    public async Task Handle_ReversibleRow_ReturnsDecryptableEnvelope()
    {
        using var db = TestSqliteContext.Create();
        var cipher = TestSeeds.CreateCipher();
        var secret = await SeedReversibleAsync(db);
        var (rsa, envelopeKey) = FakeRsa();
        var handler = new RevealSubsysAppSecretCommandHandler(db.Context, cipher, rsa);
        var appId = await db.Context.AccessApps.Where(x => x.Key == "report").Select(x => x.Id).FirstAsync(cancellationToken: CancellationToken.None);

        var response = await handler.Handle(new RevealSubsysAppSecretCommand
        {
            Id = appId,
            EncryptedKey = "whatever-cipher",
        }, CancellationToken.None);

        Assert.Equal(cipher.Mask(secret), response.Mask);
        Assert.NotEmpty(response.Iv);
        Assert.NotEmpty(response.Data);

        // 前端同款解密：AES-256-GCM(iv, data)
        var payload = Convert.FromBase64String(response.Data);
        var cipherBytes = payload[..^16];
        var tag = payload[^16..];
        var plain = new byte[cipherBytes.Length];
        using var gcm = new AesGcm(envelopeKey, 16);
        gcm.Decrypt(Convert.FromBase64String(response.Iv), cipherBytes, tag, plain);
        Assert.Equal(secret, Encoding.UTF8.GetString(plain));
    }

    [Fact]
    public async Task Handle_LegacyHashRow_Throws400()
    {
        using var db = TestSqliteContext.Create();
        // 存量行：无前缀 PBKDF2 哈希不可回显
        var (hash, _) = MoAI.Infra.Helpers.PBKDF2Helper.ToHash("whatever");
        var entity = await TestSeeds.SeedSubsysAppAsync(db.Context, "legacy", secret: hash, secretMask: string.Empty);
        var (rsa, _) = FakeRsa();
        var handler = new RevealSubsysAppSecretCommandHandler(db.Context, TestSeeds.CreateCipher(), rsa);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new RevealSubsysAppSecretCommand
        {
            Id = entity.Id,
            EncryptedKey = "whatever-cipher",
        }, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_BadEnvelopeKeyLength_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var cipher = TestSeeds.CreateCipher();
        await SeedReversibleAsync(db);
        var appId = await db.Context.AccessApps.Where(x => x.Key == "report").Select(x => x.Id).FirstAsync(cancellationToken: CancellationToken.None);
        var shortKey = RandomNumberGenerator.GetBytes(8);
        var rsa = new Mock<IRsaProvider>();
        rsa.Setup(x => x.Decrypt(It.IsAny<string>(), null)).Returns(Convert.ToBase64String(shortKey));
        var handler = new RevealSubsysAppSecretCommandHandler(db.Context, cipher, rsa.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new RevealSubsysAppSecretCommand
        {
            Id = appId,
            EncryptedKey = "whatever-cipher",
        }, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_MissingApp_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var (rsa, _) = FakeRsa();
        var handler = new RevealSubsysAppSecretCommandHandler(db.Context, TestSeeds.CreateCipher(), rsa);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new RevealSubsysAppSecretCommand
        {
            Id = Guid.CreateVersion7(),
            EncryptedKey = "whatever-cipher",
        }, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }
}
