using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.External.Commands;
using MoAI.External.Commands.Responses;
using MoAI.External.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.External.Handlers;

/// <summary>
/// <inheritdoc cref="ExternalAppLoginCommand"/>
/// </summary>
public class ExternalAppLoginCommandHandler : IRequestHandler<ExternalAppLoginCommand, ExternalAppLoginCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalTokenProvider _externalTokenProvider;
    private readonly ILogger<ExternalAppLoginCommandHandler> _logger;

    /// <summary>
    /// 外部接入 Token 有效期（小时）.
    /// </summary>
    private const int ExternalTokenExpirationHours = 2;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalAppLoginCommandHandler"/> class.
    /// </summary>
    public ExternalAppLoginCommandHandler(
        DatabaseContext databaseContext,
        IExternalTokenProvider externalTokenProvider,
        ILogger<ExternalAppLoginCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _externalTokenProvider = externalTokenProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExternalAppLoginCommandResponse> Handle(ExternalAppLoginCommand request, CancellationToken cancellationToken)
    {
        // 查找外部应用
        var externalApp = await _databaseContext.ExternalApps
            .Where(x => x.Id == request.AppId && x.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (externalApp == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        // 验证密钥
        if (externalApp.Key != request.Key)
        {
            throw new BusinessException("应用密钥错误") { StatusCode = 401 };
        }

        // 检查是否禁用
        if (externalApp.IsDsiable)
        {
            throw new BusinessException("应用已被禁用") { StatusCode = 403 };
        }

        // 生成 Token
        var (accessToken, refreshToken) = _externalTokenProvider.GenerateExternalAppTokens(
            externalApp.Id,
            externalApp.Name,
            externalApp.TeamId);

        _logger.LogInformation("External app login success. {@Message}", new { externalApp.Id, externalApp.Name, externalApp.TeamId });

        return new ExternalAppLoginCommandResponse
        {
            AppId = externalApp.Id,
            AppName = externalApp.Name,
            TeamId = externalApp.TeamId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = DateTimeOffset.Now.AddHours(ExternalTokenExpirationHours).ToUnixTimeMilliseconds(),
        };
    }
}
