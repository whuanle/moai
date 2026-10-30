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
/// <inheritdoc cref="SystemAppTokenCommand"/>
/// </summary>
public class SystemAppTokenCommandHandler : IRequestHandler<SystemAppTokenCommand, SubsystemTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemTokenProvider _subsystemTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAppTokenCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="subsystemTokenProvider">token 签发服务.</param>
    public SystemAppTokenCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider subsystemTokenProvider)
    {
        _databaseContext = databaseContext;
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemTokenCommandResponse> Handle(SystemAppTokenCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Key == request.AppId, cancellationToken);
        if (app == null || string.IsNullOrEmpty(app.Secret)
            || !PBKDF2Helper.VerifyHash(request.Secret, app.Secret, app.SecretSalt ?? string.Empty))
        {
            throw new BusinessException("接入 appid 或密钥无效.") { StatusCode = 401 };
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            throw new BusinessException("系统应用接入已停用，拒绝换取 token.") { StatusCode = 403 };
        }

        var (accessToken, refreshToken, expiresIn) = _subsystemTokenProvider.GenerateSystemAppTokens(app);
        return new SubsystemTokenCommandResponse { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn, TokenType = "Bearer" };
    }
}

/// <summary>
/// <inheritdoc cref="SystemAppTokenRefreshCommand"/>
/// </summary>
public class SystemAppTokenRefreshCommandHandler : IRequestHandler<SystemAppTokenRefreshCommand, SubsystemTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemTokenProvider _subsystemTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAppTokenRefreshCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="subsystemTokenProvider">token 签发服务.</param>
    public SystemAppTokenRefreshCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider subsystemTokenProvider)
    {
        _databaseContext = databaseContext;
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemTokenCommandResponse> Handle(SystemAppTokenRefreshCommand request, CancellationToken cancellationToken)
    {
        // refresh token 无状态不落库，吊销 = 刷新时按数据库当前状态重验（停用 403/删除或 appid 不匹配 401），刷新即轮换
        if (!_subsystemTokenProvider.TryValidateSystemAppRefreshToken(request.RefreshToken, out var appId))
        {
            throw new BusinessException("refresh token 无效或已过期.") { StatusCode = 401 };
        }

        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == appId, cancellationToken);
        if (app == null)
        {
            throw new BusinessException("系统应用接入不存在或已删除，token 已吊销.") { StatusCode = 401 };
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            throw new BusinessException("系统应用接入已停用，token 已吊销.") { StatusCode = 403 };
        }

        var (accessToken, refreshToken, expiresIn) = _subsystemTokenProvider.GenerateSystemAppTokens(app);
        return new SubsystemTokenCommandResponse { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn, TokenType = "Bearer" };
    }
}
