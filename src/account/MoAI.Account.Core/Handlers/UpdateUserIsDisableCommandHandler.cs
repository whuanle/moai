using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Commands;
using MoAI.Database;
using MoAI.Infra.Events;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Account.Services;
using MoAI.Message.Models;
using MoAI.Message.Services;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserIsDisableCommand"/>
/// </summary>
public class UpdateUserIsDisableCommandHandler : IRequestHandler<UpdateUserIsDisableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserSecurityStateService _securityStateService;
    private readonly IAccessEventPublisher _eventPublisher;
    private readonly IEventPushService _eventPushService;
    private readonly ILogger<UpdateUserIsDisableCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserIsDisableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    /// <param name="securityStateService">用户安全态分布式缓存.</param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    /// <param name="eventPushService">站内用户实时事件推送.</param>
    /// <param name="logger">日志.</param>
    public UpdateUserIsDisableCommandHandler(
        DatabaseContext databaseContext,
        IUserAccountService userAccountService,
        IUserSecurityStateService securityStateService,
        IAccessEventPublisher eventPublisher,
        IEventPushService eventPushService,
        ILogger<UpdateUserIsDisableCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _securityStateService = securityStateService;
        _eventPublisher = eventPublisher;
        _eventPushService = eventPushService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserIsDisableCommand request, CancellationToken cancellationToken)
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

        if (user.Id.ToString() == rootValue)
        {
            throw new BusinessException("不能禁用超级管理员账号.") { StatusCode = 400 };
        }

        if (user.Id == request.ContextUserId)
        {
            throw new BusinessException("不能禁用自己的账号.") { StatusCode = 400 };
        }

        if (user.IsAdmin && request.ContextUserId.ToString() != rootValue)
        {
            throw new BusinessException("管理员不能操作其他管理员账号.") { StatusCode = 403 };
        }

        var changed = user.IsDisable != request.IsDisable;
        if (changed)
        {
            // 状态切换即吊销该用户全部既有会话：token 携带签发时版本，版本不一致在任意实例上立即失效
            user.AuthVersion++;
        }

        user.IsDisable = request.IsDisable;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _securityStateService.SetAsync(user.Id, user.IsDisable, false, user.AuthVersion, cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);
        await _eventPublisher.PublishUserStatusChangedAsync(user.Id, user.UserName, user.IsDisable, user.AuthVersion, cancellationToken);

        if (changed)
        {
            await NotifySessionRevokedAsync(user.Id, user.AuthVersion, user.IsDisable ? "disabled" : "enabled", cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 在线用户实时下线信号：仅推事件名与版本，前端收到后清登录态回登录页；推送失败只记日志，不影响业务结果.
    /// </summary>
    private async Task NotifySessionRevokedAsync(long userId, long authVersion, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _eventPushService.PushToUsersAsync(
                [userId],
                EventNames.AuthRevoked,
                new Dictionary<string, string>
                {
                    ["reason"] = reason,
                    ["authVersion"] = authVersion.ToString(),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "用户会话吊销事件推送失败. UserId={UserId}, Reason={Reason}", userId, reason);
        }
    }
}
