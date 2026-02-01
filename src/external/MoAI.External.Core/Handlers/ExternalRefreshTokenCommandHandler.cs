using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using MoAI.Database;
using MoAI.External.Commands;
using MoAI.External.Commands.Responses;
using MoAI.External.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.External.Handlers;

/// <summary>
/// <inheritdoc cref="ExternalRefreshTokenCommand"/>
/// </summary>
public class ExternalRefreshTokenCommandHandler : IRequestHandler<ExternalRefreshTokenCommand, ExternalRefreshTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalTokenProvider _externalTokenProvider;
    private readonly ILogger<ExternalRefreshTokenCommandHandler> _logger;

    /// <summary>
    /// 外部接入 Token 有效期（小时）.
    /// </summary>
    private const int ExternalTokenExpirationHours = 2;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalRefreshTokenCommandHandler"/> class.
    /// </summary>
    public ExternalRefreshTokenCommandHandler(
        DatabaseContext databaseContext,
        IExternalTokenProvider externalTokenProvider,
        ILogger<ExternalRefreshTokenCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _externalTokenProvider = externalTokenProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExternalRefreshTokenCommandResponse> Handle(ExternalRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 验证 Token
        var tokenValidationResult = await _externalTokenProvider.ValidateTokenAsync(request.RefreshToken);

        if (!tokenValidationResult.IsValid)
        {
            throw new BusinessException("Token 验证失败") { StatusCode = 401 };
        }

        // 解析 Token
        var (userContext, claims) = await _externalTokenProvider.ParseUserContextFromTokenAsync(request.RefreshToken);

        // 验证是否是 refresh_token
        if (!claims.TryGetValue("token_type", out var tokenType) ||
            string.IsNullOrEmpty(tokenType.Value) ||
            !"refresh_token".Equals(tokenType.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("非 refresh_token") { StatusCode = 401 };
        }

        // 验证是否是外部接入类型
        if (userContext.UserType != UserType.ExternalApp && userContext.UserType != UserType.External)
        {
            throw new BusinessException("非外部接入 Token") { StatusCode = 401 };
        }

        // 获取团队ID
        if (!userContext.Properties.TryGetValue("team_id", out var teamIdStr) || !int.TryParse(teamIdStr, out var teamId))
        {
            throw new BusinessException("无法获取团队信息") { StatusCode = 400 };
        }

        string accessToken;
        string refreshToken;

        if (userContext.UserType == UserType.ExternalApp)
        {
            // 外部应用刷新 Token
            var subject = claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subClaim) ? subClaim.Value : string.Empty;
            if (!Guid.TryParse(subject, out var appId))
            {
                throw new BusinessException("无效的应用ID") { StatusCode = 400 };
            }

            // 验证应用是否存在且未禁用
            var externalApp = await _databaseContext.ExternalApps
                .Where(x => x.Id == appId && x.IsDeleted == 0)
                .FirstOrDefaultAsync(cancellationToken);

            if (externalApp == null)
            {
                throw new BusinessException("应用不存在") { StatusCode = 404 };
            }

            if (externalApp.IsDsiable)
            {
                throw new BusinessException("应用已被禁用") { StatusCode = 403 };
            }

            (accessToken, refreshToken) = _externalTokenProvider.GenerateExternalAppTokens(
                externalApp.Id,
                externalApp.Name,
                externalApp.TeamId);

            _logger.LogInformation("External app refresh token success. {@Message}", new { externalApp.Id, externalApp.Name });
        }
        else
        {
            // 外部用户刷新 Token
            var externalUserId = userContext.Properties.TryGetValue("external_user_id", out var extUserId) ? extUserId : string.Empty;
            if (string.IsNullOrEmpty(externalUserId))
            {
                externalUserId = claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subClaim) ? subClaim.Value : string.Empty;
            }

            (accessToken, refreshToken) = _externalTokenProvider.GenerateExternalUserTokens(
                externalUserId,
                userContext.UserName,
                userContext.NickName,
                userContext.Email,
                teamId);

            _logger.LogInformation("External user refresh token success. {@Message}", new { externalUserId, userContext.UserName });
        }

        return new ExternalRefreshTokenCommandResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = DateTimeOffset.Now.AddHours(ExternalTokenExpirationHours).ToUnixTimeMilliseconds(),
        };
    }
}
