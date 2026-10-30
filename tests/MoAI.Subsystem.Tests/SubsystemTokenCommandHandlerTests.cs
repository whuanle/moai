using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Handlers;
using MoAI.Subsystem.Services;
using Moq;
using Xunit;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 子系统接入 token 换取处理器测试.
/// </summary>
public class SubsystemTokenCommandHandlerTests
{
    private static async Task<SubsystemEntity> SeedSubsystemAsync(DatabaseContext context, string key = "report", SubsystemStatus status = SubsystemStatus.Enabled)
    {
        var (hash, salt) = PBKDF2Helper.ToHash("secret-000");
        var entity = new SubsystemEntity
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Name = "智能报表",
            Description = string.Empty,
            Secret = hash,
            SecretSalt = salt,
            Status = status,
            TeamScope = SubsystemTeamScope.AllTeams,
        };
        context.Subsystems.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private static SubsystemTokenCommandHandler CreateHandler(DatabaseContext context)
    {
        var tokenProvider = new Mock<ISubsystemTokenProvider>();
        tokenProvider.Setup(x => x.GenerateMachineTokens(It.IsAny<Database.Entities.SubsystemEntity>())).Returns(("token", "refresh", 7200));
        return new SubsystemTokenCommandHandler(context, tokenProvider.Object);
    }

    [Fact]
    public async Task Handle_CorrectSecret_Succeeds()
    {
        using var db = TestSqliteContext.Create();
        await SeedSubsystemAsync(db.Context);
        var handler = CreateHandler(db.Context);

        var response = await handler.Handle(new SubsystemTokenCommand { Key = "report", Secret = "secret-000" }, CancellationToken.None);
        Assert.Equal("Bearer", response.TokenType);
        Assert.True(response.ExpiresIn > 0);
    }

    [Fact]
    public async Task Handle_WrongSecret_Throws401()
    {
        using var db = TestSqliteContext.Create();
        await SeedSubsystemAsync(db.Context);
        var handler = CreateHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { Key = "report", Secret = "wrong" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_UnknownKey_Throws401()
    {
        using var db = TestSqliteContext.Create();
        await SeedSubsystemAsync(db.Context);
        var handler = CreateHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { Key = "nope", Secret = "secret-000" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_Disabled_Throws403()
    {
        using var db = TestSqliteContext.Create();
        await SeedSubsystemAsync(db.Context, status: SubsystemStatus.Disabled);
        var handler = CreateHandler(db.Context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { Key = "report", Secret = "secret-000" }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_ResetSecret_OldSecretRejected()
    {
        using var db = TestSqliteContext.Create();
        var entity = await SeedSubsystemAsync(db.Context);
        var resetHandler = new ResetSubsystemSecretCommandHandler(db.Context);
        var resetResult = await resetHandler.Handle(new ResetSubsystemSecretCommand { SubsystemId = entity.Id }, CancellationToken.None);

        // 旧密钥失效 401，新密钥可用
        var handler = CreateHandler(db.Context);
        var oldEx = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(new SubsystemTokenCommand { Key = "report", Secret = "secret-000" }, CancellationToken.None));
        Assert.Equal(401, oldEx.StatusCode);

        await handler.Handle(new SubsystemTokenCommand { Key = "report", Secret = resetResult.Secret }, CancellationToken.None);
    }
}
