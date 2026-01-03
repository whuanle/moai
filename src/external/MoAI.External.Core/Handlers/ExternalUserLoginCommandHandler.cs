using MediatR;
using Microsoft.Extensions.Logging;
using MoAI.External.Commands;
using MoAI.External.Commands.Responses;
using MoAI.External.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.External.Handlers;

/// <summary>
/// <inheritdoc cref="ExternalUserLoginCommand"/>
/// </summary>
public class ExternalUserLoginCommandHandler : IRequestHandler<ExternalUserLoginCommand, ExternalUserLoginCommandResponse>
{
    private readonly IExternalTokenProvider _externalTokenProvider;
    private readonly UserContext _userContext;
    private readonly ILogger<ExternalUserLoginCommandHandler> _logger;

    /// <summary>
    /// 外部接入 Token 有效期（小时）.
    /// </summary>
    private const int ExternalTokenExpirationHours = 2;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalUserLoginCommandHandler"/> class.
    /// </summary>
    public ExternalUserLoginCommandHandler(
        IExternalTokenProvider externalTokenProvider,
        UserContext userContext,
        ILogger<ExternalUserLoginCommandHandler> logger)
    {
        _externalTokenProvider = externalTokenProvider;
        _userContext = userContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExternalUserLoginCommandResponse> Handle(ExternalUserLoginCommand request, CancellationToken cancellationToken)
    {
        // 验证调用者必须是外部应用
        if (_userContext.UserType != UserType.ExternalApp)
        {
            throw new BusinessException("只有外部应用才能为外部用户颁发 Token") { StatusCode = 403 };
        }

        // 从当前上下文获取团队ID
        if (!_userContext.Properties.TryGetValue("team_id", out var teamIdStr) || !int.TryParse(teamIdStr, out var teamId))
        {
            throw new BusinessException("无法获取团队信息") { StatusCode = 400 };
        }

        // 生成外部用户 Token
        var (accessToken, refreshToken) = _externalTokenProvider.GenerateExternalUserTokens(
            request.ExternalUserId,
            request.UserName,
            request.NickName,
            request.Email,
            teamId);

        _logger.LogInformation("External user login success. {@Message}", new { request.ExternalUserId, request.UserName, teamId });

        return await Task.FromResult(new ExternalUserLoginCommandResponse
        {
            ExternalUserId = request.ExternalUserId,
            UserName = request.UserName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = DateTimeOffset.UtcNow.AddHours(ExternalTokenExpirationHours).ToUnixTimeMilliseconds(),
        });
    }
}
