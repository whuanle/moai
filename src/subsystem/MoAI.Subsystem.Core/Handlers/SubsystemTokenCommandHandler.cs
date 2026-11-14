using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
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
    private readonly ISubsystemSecretCipher _secretCipher;
    private readonly ISubsysTokenRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemTokenCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="subsystemTokenProvider">接入 token 服务.</param>
    /// <param name="secretCipher">密钥校验服务（v2 可逆比对/存量 PBKDF2）.</param>
    /// <param name="rateLimiter">换取限流服务（公网暴力枚举防护）.</param>
    public SubsystemTokenCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider subsystemTokenProvider, ISubsystemSecretCipher secretCipher, ISubsysTokenRateLimiter rateLimiter)
    {
        _databaseContext = databaseContext;
        _subsystemTokenProvider = subsystemTokenProvider;
        _secretCipher = secretCipher;
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc/>
    public async Task<SubsystemTokenCommandResponse> Handle(SubsystemTokenCommand request, CancellationToken cancellationToken)
    {
        // 公网暴力枚举防护：按 (IP, appid) 与 IP 两维度限制失败次数，超限窗口内直接 429 不再校验
        var clientIp = _rateLimiter.GetClientIp();
        if (_rateLimiter.IsBlocked(clientIp, request.AppId))
        {
            throw new BusinessException("请求过于频繁，请稍后再试.") { StatusCode = 429 };
        }

        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Key == request.AppId, cancellationToken);
        if (app == null || string.IsNullOrEmpty(app.Secret)
            || !_secretCipher.Verify(app.Secret, app.SecretSalt ?? string.Empty, request.Secret))
        {
            await _rateLimiter.CountFailureAsync(clientIp, request.AppId);
            throw new BusinessException("接入 appid 或密钥无效.") { StatusCode = 401 };
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            throw new BusinessException("接入已停用，拒绝换取 token.") { StatusCode = 403 };
        }

        var (accessToken, refreshToken, expiresIn) = _subsystemTokenProvider.GenerateMachineTokens(app);
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
    /// <param name="subsystemTokenProvider">接入 token 服务.</param>
    public SubsystemTokenRefreshCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider subsystemTokenProvider)
    {
        _databaseContext = databaseContext;
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemTokenCommandResponse> Handle(SubsystemTokenRefreshCommand request, CancellationToken cancellationToken)
    {
        // refresh token 无状态不落库，吊销 = 刷新时按数据库当前状态重验（停用 403/删除或 appid 不匹配 401），刷新即轮换
        if (!_subsystemTokenProvider.TryValidateMachineRefreshToken(request.RefreshToken, out var appId, out var appKey))
        {
            throw new BusinessException("refresh token 无效或已过期.") { StatusCode = 401 };
        }

        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == appId, cancellationToken);
        if (app == null || !string.Equals(app.Key, appKey, StringComparison.Ordinal))
        {
            throw new BusinessException("接入不存在或已删除，token 已吊销.") { StatusCode = 401 };
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            throw new BusinessException("接入已停用，token 已吊销.") { StatusCode = 403 };
        }

        var (accessToken, refreshToken, expiresIn) = _subsystemTokenProvider.GenerateMachineTokens(app);
        return new SubsystemTokenCommandResponse { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn, TokenType = "Bearer" };
    }
}
