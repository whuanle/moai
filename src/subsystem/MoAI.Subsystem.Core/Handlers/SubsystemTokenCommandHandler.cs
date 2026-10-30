using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SubsystemTokenCommand"/>
/// </summary>
public class SubsystemTokenCommandHandler : IRequestHandler<SubsystemTokenCommand, SubsystemTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemTokenProvider _subsystemTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemTokenCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="subsystemTokenProvider">子系统 token 服务.</param>
    public SubsystemTokenCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider subsystemTokenProvider)
    {
        _databaseContext = databaseContext;
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemTokenCommandResponse> Handle(SubsystemTokenCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Key == request.Key, cancellationToken);
        if (subsystem == null || !PBKDF2Helper.VerifyHash(request.Secret, subsystem.Secret, subsystem.SecretSalt))
        {
            throw new BusinessException("子系统标识或接入密钥无效.") { StatusCode = 401 };
        }

        if (subsystem.Status == SubsystemStatus.Disabled)
        {
            throw new BusinessException("子系统已停用，拒绝换取接入 token.") { StatusCode = 403 };
        }

        var (accessToken, refreshToken, expiresIn) = _subsystemTokenProvider.GenerateMachineTokens(subsystem);
        return new SubsystemTokenCommandResponse { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn, TokenType = "Bearer" };
    }
}

/// <summary>
/// <inheritdoc cref="SubsystemTokenRefreshCommand"/>
/// </summary>
public class SubsystemTokenRefreshCommandHandler : IRequestHandler<SubsystemTokenRefreshCommand, SubsystemTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemTokenProvider _subsystemTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemTokenRefreshCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="subsystemTokenProvider">子系统 token 服务.</param>
    public SubsystemTokenRefreshCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider subsystemTokenProvider)
    {
        _databaseContext = databaseContext;
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemTokenCommandResponse> Handle(SubsystemTokenRefreshCommand request, CancellationToken cancellationToken)
    {
        // refresh token 无状态不落库，吊销 = 刷新时按数据库当前状态重验（停用 403/删除或 appid 不匹配 401），刷新即轮换
        if (!_subsystemTokenProvider.TryValidateMachineRefreshToken(request.RefreshToken, out var subsystemId, out var subsystemKey))
        {
            throw new BusinessException("refresh token 无效或已过期.") { StatusCode = 401 };
        }

        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Id == subsystemId, cancellationToken);
        if (subsystem == null || !string.Equals(subsystem.Key, subsystemKey, StringComparison.Ordinal))
        {
            throw new BusinessException("子系统不存在或已删除，token 已吊销.") { StatusCode = 401 };
        }

        if (subsystem.Status == SubsystemStatus.Disabled)
        {
            throw new BusinessException("子系统已停用，token 已吊销.") { StatusCode = 403 };
        }

        var (accessToken, refreshToken, expiresIn) = _subsystemTokenProvider.GenerateMachineTokens(subsystem);
        return new SubsystemTokenCommandResponse { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn, TokenType = "Bearer" };
    }
}
