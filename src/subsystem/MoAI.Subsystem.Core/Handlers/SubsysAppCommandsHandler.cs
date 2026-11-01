using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Commands.Responses;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="CreateSubsysAppCommand"/>
/// </summary>
public class CreateSubsysAppCommandHandler : IRequestHandler<CreateSubsysAppCommand, CreateSubsysAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemSecretCipher _secretCipher;
    private readonly ISubsystemAppIdCodec _appIdCodec;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSubsysAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="secretCipher">密钥可逆加密服务.</param>
    /// <param name="appIdCodec">结构化 appid 编解码服务.</param>
    public CreateSubsysAppCommandHandler(DatabaseContext databaseContext, ISubsystemSecretCipher secretCipher, ISubsystemAppIdCodec appIdCodec)
    {
        _databaseContext = databaseContext;
        _secretCipher = secretCipher;
        _appIdCodec = appIdCodec;
    }

    /// <inheritdoc/>
    public async Task<CreateSubsysAppCommandResponse> Handle(CreateSubsysAppCommand request, CancellationToken cancellationToken)
    {
        bool isSubsystem = request.AppType == AccessAppType.Subsystem;
        var entityId = Guid.CreateVersion7();

        // 子系统 appid 自填且不可修改，先查重；应用接入 appid 自动生成（moai-sa- 前缀）
        string key;
        if (isSubsystem)
        {
            var keyExists = await _databaseContext.AccessApps.AnyAsync(x => x.Key == request.Key, cancellationToken);
            if (keyExists)
            {
                throw new BusinessException($"接入标识 {request.Key} 已存在.") { StatusCode = 409 };
            }

            key = request.Key;
        }
        else
        {
            // 结构化 appid：编码实体 Id + HMAC 绑定（本地防伪造，公网扫描不打库）
            key = _appIdCodec.New(entityId);
        }

        var teamIds = await SubsystemTeamsGuard.NormalizeAsync(request.TeamScope, request.TeamIds, _databaseContext, cancellationToken);

        // 密钥以 AES 可逆密文落库（v2: 前缀，可随时回显），列表只回显掩码；系统级接入不设资源范围（scopes=0，全量访问协作面）
        var secret = _secretCipher.NewSecret();

        var entity = new AccessAppEntity
        {
            Id = entityId,
            Level = AccessAppLevel.System,
            AppType = request.AppType,
            TeamId = 0,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Key = key,
            KeyPrefix = key[..Math.Min(key.Length, SubsystemAuthDefaults.KeyPrefixLength)],
            Secret = _secretCipher.EncryptForStorage(secret),
            SecretSalt = string.Empty,
            SecretMask = _secretCipher.Mask(secret),
            Scopes = 0,
            Status = AccessAppStatus.Enabled,
            CallbackUrls = isSubsystem ? SubsystemCallbacks.Serialize(request.CallbackUrls) : "[]",
        };
        await _databaseContext.AccessApps.AddAsync(entity, cancellationToken);

        if (isSubsystem)
        {
            foreach (var teamId in teamIds)
            {
                _databaseContext.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity
                {
                    Id = Guid.CreateVersion7(),
                    SubsystemId = entity.Id,
                    TeamId = teamId,
                });
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new CreateSubsysAppCommandResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            AppType = entity.AppType,
            Key = key,
            SecretMask = entity.SecretMask,
            KeyPrefix = entity.KeyPrefix,
        };
    }
}

/// <summary>
/// <inheritdoc cref="UpdateSubsysAppCommand"/>
/// </summary>
public class UpdateSubsysAppCommandHandler : IRequestHandler<UpdateSubsysAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSubsysAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    public UpdateSubsysAppCommandHandler(DatabaseContext databaseContext, ISubsystemOnlineService onlineService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSubsysAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new BusinessException("接入不存在.") { StatusCode = 404 };

        app.Name = request.Name;
        app.Description = request.Description ?? string.Empty;
        app.Status = request.Status;

        if (app.AppType == AccessAppType.Subsystem)
        {
            var teamIds = await SubsystemTeamsGuard.NormalizeAsync(request.TeamScope, request.TeamIds, _databaseContext, cancellationToken);
            app.TeamScope = request.TeamScope;
            app.CallbackUrls = SubsystemCallbacks.Serialize(request.CallbackUrls);

            // 授权团队列表整体覆盖：全员范围清空，指定范围按差集增删
            var existingAuths = await _databaseContext.SubsystemTeamAuths
                .Where(x => x.SubsystemId == app.Id)
                .ToListAsync(cancellationToken);
            var removeAuths = request.TeamScope == SubsystemTeamScope.AllTeams
                ? existingAuths
                : existingAuths.Where(x => !teamIds.Contains(x.TeamId)).ToList();
            _databaseContext.SubsystemTeamAuths.RemoveRange(removeAuths);

            var addTeamIds = teamIds.Where(id => existingAuths.All(x => x.TeamId != id));
            foreach (var teamId in addTeamIds)
            {
                _databaseContext.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity
                {
                    Id = Guid.CreateVersion7(),
                    SubsystemId = app.Id,
                    TeamId = teamId,
                });
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 停用即时生效：清除在线在场集合，机器面调用随中间件实时校验一并拒绝
        if (request.Status == AccessAppStatus.Disabled)
        {
            await _onlineService.RemoveAsync(app.Id);
        }

        return EmptyCommandResponse.Default;
    }
}

/// <summary>
/// <inheritdoc cref="ResetSubsysAppSecretCommand"/>
/// </summary>
public class ResetSubsysAppSecretCommandHandler : IRequestHandler<ResetSubsysAppSecretCommand, ResetSubsysAppSecretCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemSecretCipher _secretCipher;
    private readonly ISubsysTokenRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetSubsysAppSecretCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="secretCipher">密钥可逆加密服务.</param>
    /// <param name="rateLimiter">换取限流服务.</param>
    public ResetSubsysAppSecretCommandHandler(DatabaseContext databaseContext, ISubsystemSecretCipher secretCipher, ISubsysTokenRateLimiter rateLimiter)
    {
        _databaseContext = databaseContext;
        _secretCipher = secretCipher;
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc/>
    public async Task<ResetSubsysAppSecretCommandResponse> Handle(ResetSubsysAppSecretCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new BusinessException("接入不存在.") { StatusCode = 404 };

        // 新密钥 AES 可逆密文落库（转为 v2 可回显格式），旧密钥立即失效；已签发的短时 access token 依赖回库实时校验，停用/删除即时拒绝
        var secret = _secretCipher.NewSecret();
        app.Secret = _secretCipher.EncryptForStorage(secret);
        app.SecretSalt = string.Empty;
        app.SecretMask = _secretCipher.Mask(secret);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 新密钥换取不被旧失败计数误伤（清限流窗口）
        await _rateLimiter.ResetAllAsync();
        return new ResetSubsysAppSecretCommandResponse { SecretMask = app.SecretMask };
    }
}

/// <summary>
/// <inheritdoc cref="DeleteSubsysAppCommand"/>
/// </summary>
public class DeleteSubsysAppCommandHandler : IRequestHandler<DeleteSubsysAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemOnlineService _onlineService;
    private readonly ISubsysTokenRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSubsysAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    /// <param name="rateLimiter">换取限流服务.</param>
    public DeleteSubsysAppCommandHandler(DatabaseContext databaseContext, ISubsystemOnlineService onlineService, ISubsysTokenRateLimiter rateLimiter)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteSubsysAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (app == null)
        {
            return EmptyCommandResponse.Default;
        }

        // 软删除后 appid 释放（唯一索引按 (key, is_deleted)），密钥与已签发 token 立即失效
        await _databaseContext.SoftDeleteAsync(_databaseContext.AccessApps.Where(x => x.Id == app.Id));

        var auths = await _databaseContext.SubsystemTeamAuths
            .Where(x => x.SubsystemId == app.Id)
            .ToListAsync(cancellationToken);
        _databaseContext.SubsystemTeamAuths.RemoveRange(auths);

        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _onlineService.RemoveAsync(app.Id);
        // 同名 appid 重建不被残留限流计数误伤
        await _rateLimiter.ResetAllAsync();
        return EmptyCommandResponse.Default;
    }
}
