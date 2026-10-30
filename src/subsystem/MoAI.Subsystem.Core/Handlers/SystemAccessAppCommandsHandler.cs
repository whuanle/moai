using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Commands.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="CreateSystemAccessAppCommand"/>
/// </summary>
public class CreateSystemAccessAppCommandHandler : IRequestHandler<CreateSystemAccessAppCommand, CreateSystemAccessAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSystemAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateSystemAccessAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateSystemAccessAppCommandResponse> Handle(CreateSystemAccessAppCommand request, CancellationToken cancellationToken)
    {
        if (!SystemApiKeyScopeCodes.TryParseCodes(request.Scopes, out var scopes))
        {
            throw new BusinessException("功能范围代码无效或为空.") { StatusCode = 400 };
        }

        // appid 原文列内明文存储（Bearer 直连凭证），密钥只存 PBKDF2 哈希，二者仅在创建结果中返回一次
        var (key, keyPrefix) = SystemAccessAppKeyGenerator.New();
        var secret = SubsystemSecretGenerator.New();
        var (secretHash, secretSalt) = PBKDF2Helper.ToHash(secret);
        var entity = new AccessAppEntity
        {
            Id = Guid.CreateVersion7(),
            Level = AccessAppLevel.System,
            TeamId = 0,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Key = key,
            KeyPrefix = keyPrefix,
            Secret = secretHash,
            SecretSalt = secretSalt,
            Scopes = (int)scopes,
            Status = AccessAppStatus.Enabled,
        };
        await _databaseContext.AccessApps.AddAsync(entity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateSystemAccessAppCommandResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Key = key,
            Secret = secret,
            KeyPrefix = keyPrefix,
        };
    }
}

/// <summary>
/// <inheritdoc cref="UpdateSystemAccessAppCommand"/>
/// </summary>
public class UpdateSystemAccessAppCommandHandler : IRequestHandler<UpdateSystemAccessAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSystemAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateSystemAccessAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSystemAccessAppCommand request, CancellationToken cancellationToken)
    {
        if (!SystemApiKeyScopeCodes.TryParseCodes(request.Scopes, out var scopes))
        {
            throw new BusinessException("功能范围代码无效或为空.") { StatusCode = 400 };
        }

        var entity = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new BusinessException("系统应用接入不存在.") { StatusCode = 404 };

        entity.Name = request.Name;
        entity.Description = request.Description ?? string.Empty;
        entity.Scopes = (int)scopes;
        entity.Status = request.Status;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}

/// <summary>
/// <inheritdoc cref="ResetSystemAccessAppSecretCommand"/>
/// </summary>
public class ResetSystemAccessAppSecretCommandHandler : IRequestHandler<ResetSystemAccessAppSecretCommand, ResetSystemAccessAppSecretCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetSystemAccessAppSecretCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ResetSystemAccessAppSecretCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<ResetSystemAccessAppSecretCommandResponse> Handle(ResetSystemAccessAppSecretCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new BusinessException("系统应用接入不存在.") { StatusCode = 404 };

        // 新密钥重新哈希落库，旧密钥立即失效；已签发的短时 access token 依赖回库实时校验，停用/删除即时拒绝
        var secret = SubsystemSecretGenerator.New();
        var (secretHash, secretSalt) = PBKDF2Helper.ToHash(secret);
        entity.Secret = secretHash;
        entity.SecretSalt = secretSalt;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new ResetSystemAccessAppSecretCommandResponse { Secret = secret };
    }
}

/// <summary>
/// <inheritdoc cref="DeleteSystemAccessAppCommand"/>
/// </summary>
public class DeleteSystemAccessAppCommandHandler : IRequestHandler<DeleteSystemAccessAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSystemAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteSystemAccessAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteSystemAccessAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            return EmptyCommandResponse.Default;
        }

        await _databaseContext.SoftDeleteAsync(_databaseContext.AccessApps.Where(x => x.Id == entity.Id));
        return EmptyCommandResponse.Default;
    }
}
