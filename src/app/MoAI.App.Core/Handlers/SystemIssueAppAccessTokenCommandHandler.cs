using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.App.Commands;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SystemIssueAppAccessTokenCommand"/>
/// </summary>
public class SystemIssueAppAccessTokenCommandHandler : IRequestHandler<SystemIssueAppAccessTokenCommand, SystemIssueAppAccessTokenCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalTokenProvider _externalTokenProvider;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemIssueAppAccessTokenCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalTokenProvider">外部 token 服务.</param>
    /// <param name="mediator">账户命令调度器.</param>
    public SystemIssueAppAccessTokenCommandHandler(
        DatabaseContext databaseContext,
        IExternalTokenProvider externalTokenProvider,
        IMediator mediator)
    {
        _databaseContext = databaseContext;
        _externalTokenProvider = externalTokenProvider;
        _mediator = mediator;
    }

    public async Task<SystemIssueAppAccessTokenCommandResponse> Handle(SystemIssueAppAccessTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. 第三方登录渠道必须存在（软删除由全局过滤器排除）
        var providerExists = await _databaseContext.OauthConnections
            .AnyAsync(x => x.Id == request.OauthProviderId, cancellationToken);
        if (!providerExists)
        {
            throw new BusinessException("第三方登录渠道不存在.") { StatusCode = 404 };
        }

        // 2. 目标必须是已发布、未禁用且访问点已开启的内部应用；先做目标校验，避免错误请求建出孤儿用户
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);
        var accessPointEnabled = await _databaseContext.AppAccessPoints
            .AnyAsync(x => x.AppId == request.AppId && x.Enabled, cancellationToken);
        ExternalAppAccessValidator.EnsureInternalUsable(app, accessPointEnabled);

        // 3. 第三方身份绑定匹配：同一渠道下 sub 与 unionId 任一命中即同一身份（与渠道消息归因同口径）
        var matchedUserIds = await FindMatchedUserIdsAsync(request, cancellationToken);
        var userCreated = false;
        if (matchedUserIds.Count == 0)
        {
            userCreated = await ProvisionUserAsync(request, cancellationToken);
            matchedUserIds = await FindMatchedUserIdsAsync(request, cancellationToken);
        }

        if (matchedUserIds.Count == 0)
        {
            throw new BusinessException("第三方身份初始化失败，请稍后重试.") { StatusCode = 409 };
        }

        if (matchedUserIds.Count > 1)
        {
            throw new BusinessException("第三方身份匹配到多个用户，拒绝颁发 token.") { StatusCode = 409 };
        }

        // 4. 用户必须存在且未禁用
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(x => x.Id == matchedUserIds[0], cancellationToken);
        if (user == null)
        {
            throw new BusinessException("绑定用户不存在.") { StatusCode = 404 };
        }

        if (user.IsDisable)
        {
            throw new BusinessException("用户已被禁用，拒绝颁发 token.") { StatusCode = 403 };
        }

        // 5. 颁发访问用户 token（sub=真实用户 id，绑定单应用）
        var tokens = _externalTokenProvider.GenerateAccessUserTokens(user, app!);
        return new SystemIssueAppAccessTokenCommandResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            AppId = request.AppId,
            UserCreated = userCreated,
        };
    }

    private async Task<List<long>> FindMatchedUserIdsAsync(SystemIssueAppAccessTokenCommand request, CancellationToken cancellationToken)
    {
        return await _databaseContext.UserOauthConnections
            .Where(x => x.ProviderId == request.OauthProviderId
                && (x.Sub == request.OauthSub
                    || (request.OauthUnionId != null && x.UnionId == request.OauthUnionId)))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> ProvisionUserAsync(SystemIssueAppAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var userName = await BuildProvisionedUserNameAsync(request, cancellationToken);
        try
        {
            var result = await _mediator.Send(new SystemUpsertUserCommand
            {
                UserName = userName,
                NickName = request.NickName,
                OauthProviderId = request.OauthProviderId,
                OauthSub = request.OauthSub,
                OauthUnionId = request.OauthUnionId,
            }, cancellationToken);

            return result.Created;
        }
        catch (DbUpdateException)
        {
            // 并发首次请求可能由 user_name 或 OAuth sub 的唯一索引淘汰一个事务；
            // 重新读取绑定，已收敛到同一用户时继续发 token，否则保留原始数据库异常。
            _databaseContext.ChangeTracker.Clear();
            if ((await FindMatchedUserIdsAsync(request, cancellationToken)).Count == 1)
            {
                return false;
            }

            throw;
        }
    }

    private async Task<string> BuildProvisionedUserNameAsync(SystemIssueAppAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var identityKey = Encoding.UTF8.GetBytes($"{request.OauthProviderId:N}\0{request.OauthSub}");
        var hash = Convert.ToHexString(SHA256.HashData(identityKey)).ToLowerInvariant();
        var baseName = $"oauth_{hash[..24]}";
        if (!await _databaseContext.Users.AnyAsync(x => x.UserName == baseName, cancellationToken))
        {
            return baseName;
        }

        for (var i = 0; i < 3; i++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            var candidate = $"{baseName}_{suffix}";
            if (!await _databaseContext.Users.AnyAsync(x => x.UserName == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new BusinessException("无法生成唯一的系统用户标识，请稍后重试.") { StatusCode = 409 };
    }
}
