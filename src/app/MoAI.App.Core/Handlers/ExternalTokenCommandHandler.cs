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
/// <inheritdoc cref="ExternalTokenCommand"/>
/// </summary>
public class ExternalTokenCommandHandler : IRequestHandler<ExternalTokenCommand, ExternalTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalTokenProvider _externalTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalTokenCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalTokenProvider">外部 token 服务.</param>
    public ExternalTokenCommandHandler(DatabaseContext databaseContext, IExternalTokenProvider externalTokenProvider)
    {
        _databaseContext = databaseContext;
        _externalTokenProvider = externalTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<ExternalTokenCommandResponse> Handle(ExternalTokenCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.AccessAppKey))
        {
            return await HandleWithAccessAppKeyAsync(request, cancellationToken);
        }

        // 匿名路径：is_auth=false 的外部应用直接换取 token
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId!.Value, cancellationToken);
        ExternalAppAccessValidator.EnsureUsable(app, requireNoAuth: true);

        var externalUserId = string.IsNullOrWhiteSpace(request.ExternalUserId)
            ? $"ext-{Guid.CreateVersion7().ToString("N")[..16]}"
            : request.ExternalUserId!;
        var external = await UpsertExternalAsync(app!.TeamId, accessAppId: null, externalUserId, app.Id, request.Nickname, cancellationToken);

        var tokens = _externalTokenProvider.GenerateUserTokens(external, accessApp: null);
        return new ExternalTokenCommandResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = ExternalTokenType.User,
            ExternalId = external.Id,
            ExternalUserId = external.ExternalUserId,
        };
    }

    private async Task<ExternalTokenCommandResponse> HandleWithAccessAppKeyAsync(ExternalTokenCommand request, CancellationToken cancellationToken)
    {
        var accessApp = await _databaseContext.AccessApps
            .FirstOrDefaultAsync(x => x.Key == request.AccessAppKey, cancellationToken);
        if (accessApp == null)
        {
            throw new BusinessException("应用接入 key 无效.") { StatusCode = 401 };
        }

        // 应用 token：以接入身份访问其所属团队的资源（团队级授权）
        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            if (request.AppId != null)
            {
                throw new BusinessException("获取用户 token 必须提供外部用户标识 externalUserId.") { StatusCode = 400 };
            }

            var appTokens = _externalTokenProvider.GenerateAppTokens(accessApp);
            return new ExternalTokenCommandResponse
            {
                AccessToken = appTokens.AccessToken,
                RefreshToken = appTokens.RefreshToken,
                ExpiresIn = appTokens.ExpiresIn,
                TokenType = ExternalTokenType.App,
            };
        }

        // 用户 token：绑定外部用户并仅授权单个应用
        if (request.AppId == null)
        {
            throw new BusinessException("获取用户 token 必须指定应用 id.") { StatusCode = 400 };
        }

        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId.Value, cancellationToken);
        ExternalAppAccessValidator.EnsureUsable(app);

        // 团队级授权：应用必须属于接入点所在团队
        if (app!.TeamId != accessApp.TeamId)
        {
            throw new BusinessException("应用不属于该接入点所在团队.") { StatusCode = 403 };
        }

        var external = await UpsertExternalAsync(accessApp.TeamId, accessApp.Id, request.ExternalUserId!, request.AppId.Value, request.Nickname, cancellationToken);
        var tokens = _externalTokenProvider.GenerateUserTokens(external, accessApp);
        return new ExternalTokenCommandResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = ExternalTokenType.User,
            ExternalId = external.Id,
            ExternalUserId = external.ExternalUserId,
        };
    }

    /// <summary>
    /// 按（来源接入, 外部身份标识）定位外部用户：已存在则复用（用户换绑应用即更新授权），否则新建；匿名身份（无接入）不受唯一索引约束.
    /// </summary>
    private async Task<Database.Entities.ExternalUserEntity> UpsertExternalAsync(
        int teamId,
        Guid? accessAppId,
        string externalUserId,
        Guid appId,
        string? nickname,
        CancellationToken cancellationToken)
    {
        var external = await _databaseContext.ExternalUsers
            .Where(x => x.AccessAppId == accessAppId && x.ExternalUserId == externalUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (external == null)
        {
            external = new Database.Entities.ExternalUserEntity
            {
                TeamId = teamId,
                AccessAppId = accessAppId,
                ExternalUserId = externalUserId,
            };
            _databaseContext.ExternalUsers.Add(external);
        }

        external.AppId = appId;
        external.Nickname = string.IsNullOrWhiteSpace(nickname) ? external.Nickname : nickname;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return external;
    }
}
