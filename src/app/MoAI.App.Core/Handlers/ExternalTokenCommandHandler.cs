using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
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

        // 匿名路径：外部应用一律匿名开放（is_auth 已退役），直接换取 token
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId!.Value, cancellationToken);
        ExternalAppAccessValidator.EnsureUsable(app);

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
            .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.Team && x.Key == request.AccessAppKey, cancellationToken);
        if (accessApp == null)
        {
            throw new BusinessException("应用接入 key 无效.") { StatusCode = 401 };
        }

        // 接入已设密钥则兑换必须携带并校验通过（appid 单独泄露不再足以换 token）；存量未设密钥的接入行为不变
        if (!string.IsNullOrEmpty(accessApp.Secret)
            && (string.IsNullOrEmpty(request.Secret)
                || !PBKDF2Helper.VerifyHash(request.Secret, accessApp.Secret, accessApp.SecretSalt ?? string.Empty)))
        {
            throw new BusinessException("应用接入密钥无效.") { StatusCode = 401 };
        }

        // 应用 token：以接入身份访问其所属团队的资源（团队级授权）。
        // 外部应用授权已退役：key 不再换取绑定外部用户的用户 token（身份穿透场景改走内部应用访问点）
        if (!string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            throw new BusinessException("外部应用授权已退役：外部应用一律匿名开放，如需按用户身份访问请改用内部应用访问点（POST /api/subsys/system/app-tokens）.") { StatusCode = 400 };
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
