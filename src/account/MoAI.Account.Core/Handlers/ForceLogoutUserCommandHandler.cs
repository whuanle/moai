using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Events;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Message.Models;
using MoAI.Message.Services;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="ForceLogoutUserCommand"/>
/// </summary>
public class ForceLogoutUserCommandHandler : IRequestHandler<ForceLogoutUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserSecurityStateService _securityStateService;
    private readonly IAccessEventPublisher _eventPublisher;
    private readonly IEventPushService _eventPushService;
    private readonly ILogger<ForceLogoutUserCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForceLogoutUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="securityStateService">用户安全态分布式缓存.</param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    /// <param name="eventPushService">站内用户实时事件推送.</param>
    /// <param name="logger">日志.</param>
    public ForceLogoutUserCommandHandler(
        DatabaseContext databaseContext,
        IUserAccountService userAccountService,
        IUserSecurityStateService securityStateService,
        IAccessEventPublisher eventPublisher,
        IEventPushService eventPushService,
        ILogger<ForceLogoutUserCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _securityStateService = securityStateService;
        _eventPublisher = eventPublisher;
        _eventPushService = eventPushService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ForceLogoutUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsDeleted == 0, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        var rootValue = await _databaseContext.Settings
            .Where(s => s.Key == "root")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        if (user.Id == request.ContextUserId)
        {
            throw new BusinessException("不能强制下线自己的账号.") { StatusCode = 400 };
        }

        var actorIsRoot = request.ContextUserId.ToString() == rootValue;
        if (user.Id.ToString() == rootValue && !actorIsRoot)
        {
            throw new BusinessException("不能强制下线超级管理员账号.") { StatusCode = 403 };
        }

        if (user.IsAdmin && !actorIsRoot)
        {
            throw new BusinessException("管理员不能操作其他管理员账号.") { StatusCode = 403 };
        }

        // 只递增版本、不改禁用状态：既有 access/refresh token 与外部访问用户 token 立即失效，用户可重新登录
        user.AuthVersion++;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _securityStateService.SetAsync(user.Id, user.IsDisable, user.IsDeleted > 0, user.AuthVersion, cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        await _eventPublisher.PublishUserSessionRevokedAsync(user.Id, user.UserName, user.AuthVersion, "force_logout", cancellationToken);

        try
        {
            await _eventPushService.PushToUsersAsync(
                [user.Id],
                EventNames.AuthRevoked,
                new Dictionary<string, string>
                {
                    ["reason"] = "force_logout",
                    ["authVersion"] = user.AuthVersion.ToString(),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "强制下线实时事件推送失败. UserId={UserId}", user.Id);
        }

        return EmptyCommandResponse.Default;
    }
}
