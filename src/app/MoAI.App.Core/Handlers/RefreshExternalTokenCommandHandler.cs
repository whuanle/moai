using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="RefreshExternalTokenCommand"/>
/// </summary>
public class RefreshExternalTokenCommandHandler : IRequestHandler<RefreshExternalTokenCommand, ExternalTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalTokenProvider _externalTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshExternalTokenCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalTokenProvider">外部 token 服务.</param>
    public RefreshExternalTokenCommandHandler(DatabaseContext databaseContext, IExternalTokenProvider externalTokenProvider)
    {
        _databaseContext = databaseContext;
        _externalTokenProvider = externalTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<ExternalTokenCommandResponse> Handle(RefreshExternalTokenCommand request, CancellationToken cancellationToken)
    {
        if (!_externalTokenProvider.TryParseRefreshToken(request.RefreshToken, out var tokenContext) || tokenContext == null)
        {
            throw new BusinessException("refresh token 无效或已过期.") { StatusCode = 401 };
        }

        if (tokenContext.SubjectType == UserType.ExternalApp)
        {
            return await RefreshAppTokenAsync(tokenContext, cancellationToken);
        }

        if (tokenContext.SubjectType == UserType.External)
        {
            return await RefreshUserTokenAsync(tokenContext, cancellationToken);
        }

        throw new BusinessException("refresh token 无效.") { StatusCode = 401 };
    }

    private async Task<ExternalTokenCommandResponse> RefreshAppTokenAsync(Models.ExternalTokenContext tokenContext, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tokenContext.SubjectId, out var accessAppId))
        {
            throw new BusinessException("refresh token 无效.") { StatusCode = 401 };
        }

        var accessApp = await _databaseContext.AccessApps
            .FirstOrDefaultAsync(x => x.Id == accessAppId, cancellationToken);
        if (accessApp == null)
        {
            throw new BusinessException("应用接入已被删除，token 已吊销.") { StatusCode = 401 };
        }

        var tokens = _externalTokenProvider.GenerateAppTokens(accessApp);
        return new ExternalTokenCommandResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = Models.ExternalTokenType.App,
        };
    }

    private async Task<ExternalTokenCommandResponse> RefreshUserTokenAsync(Models.ExternalTokenContext tokenContext, CancellationToken cancellationToken)
    {
        if (!long.TryParse(tokenContext.SubjectId, out var externalId))
        {
            throw new BusinessException("refresh token 无效.") { StatusCode = 401 };
        }

        var external = await _databaseContext.ExternalUsers
            .FirstOrDefaultAsync(x => x.Id == externalId, cancellationToken);
        if (external == null)
        {
            throw new BusinessException("外部用户不存在，token 已吊销.") { StatusCode = 401 };
        }

        Database.Entities.AccessAppEntity? accessApp = null;
        if (external.AccessAppId != null)
        {
            accessApp = await _databaseContext.AccessApps
                .FirstOrDefaultAsync(x => x.Id == external.AccessAppId, cancellationToken);
            if (accessApp == null)
            {
                throw new BusinessException("应用接入已被删除，token 已吊销.") { StatusCode = 401 };
            }
        }

        if (external.AppId != null)
        {
            var app = await _databaseContext.Apps
                .FirstOrDefaultAsync(x => x.Id == external.AppId, cancellationToken);

            // 匿名身份（无接入）刷新时要求应用仍无需授权；有接入的身份保留原授权
            ExternalAppAccessValidator.EnsureUsable(app, requireNoAuth: accessApp == null);
        }

        var tokens = _externalTokenProvider.GenerateUserTokens(external, accessApp);
        return new ExternalTokenCommandResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = Models.ExternalTokenType.User,
            ExternalId = external.Id,
            ExternalUserId = external.ExternalUserId,
        };
    }
}
