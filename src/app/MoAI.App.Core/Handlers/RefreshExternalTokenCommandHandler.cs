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

        if (tokenContext.SubjectType == UserType.Normal)
        {
            return await RefreshAccessUserTokenAsync(tokenContext, cancellationToken);
        }

        throw new BusinessException("refresh token 无效.") { StatusCode = 401 };
    }

    /// <summary>
    /// 访问用户 token 刷新：按数据库重建授权——用户须仍存在未禁用，目标内部应用可用且访问点仍开启.
    /// </summary>
    private async Task<ExternalTokenCommandResponse> RefreshAccessUserTokenAsync(Models.ExternalTokenContext tokenContext, CancellationToken cancellationToken)
    {
        if (!long.TryParse(tokenContext.SubjectId, out var userId) || tokenContext.AppId == null)
        {
            throw new BusinessException("refresh token 无效.") { StatusCode = 401 };
        }

        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在，token 已吊销.") { StatusCode = 401 };
        }

        if (user.IsDisable)
        {
            throw new BusinessException("用户已被禁用，token 已吊销.") { StatusCode = 403 };
        }

        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == tokenContext.AppId.Value, cancellationToken);
        var accessPointEnabled = await _databaseContext.AppAccessPoints
            .AnyAsync(x => x.AppId == tokenContext.AppId.Value && x.Enabled, cancellationToken);
        ExternalAppAccessValidator.EnsureInternalUsable(app, accessPointEnabled);

        var tokens = _externalTokenProvider.GenerateAccessUserTokens(user, app!);
        return new ExternalTokenCommandResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = Models.ExternalTokenType.AccessUser,
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
        };
    }

    private async Task<ExternalTokenCommandResponse> RefreshAppTokenAsync(Models.ExternalTokenContext tokenContext, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tokenContext.SubjectId, out var subjectId))
        {
            throw new BusinessException("refresh token 无效.") { StatusCode = 401 };
        }

        var accessApp = await _databaseContext.AccessApps
            .FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
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
            ExternalAppAccessValidator.EnsureUsable(app);
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
