using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalAgentSessionCommand"/>
/// </summary>
public class CreateExternalAgentSessionCommandHandler : IRequestHandler<CreateExternalAgentSessionCommand, SimpleGuid>
{
    private const string DefaultTitle = "未命名标题";

    private readonly DatabaseContext _databaseContext;
    private readonly IAppUsageCounter _appUsageCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalAgentSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="appUsageCounter">应用使用次数计数器.</param>
    public CreateExternalAgentSessionCommandHandler(DatabaseContext databaseContext, IAppUsageCounter appUsageCounter)
    {
        _databaseContext = databaseContext;
        _appUsageCounter = appUsageCounter;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateExternalAgentSessionCommand request, CancellationToken cancellationToken)
    {
        // 访问用户 token → 真实站内用户 id（会话落 UserType.Normal，与站内账号合并）；
        // 外部用户 token / key 直连身份 → external_user.id（会话落 UserType.External）
        var (identityId, sessionUserType) = ExternalAppAccessValidator.EnsureSessionIdentity(request.Context);

        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (sessionUserType == UserType.Normal)
        {
            // 内部应用访问点：身份穿透，访问点已开启是授权前提
            var accessPointEnabled = await _databaseContext.AppAccessPoints
                .AnyAsync(x => x.AppId == request.AppId && x.Enabled, cancellationToken);
            ExternalAppAccessValidator.EnsureInternalUsable(app, accessPointEnabled);
        }
        else
        {
            ExternalAppAccessValidator.EnsureUsable(app);
        }

        // 团队级授权：应用必须属于 token 归属团队
        if ((long)app!.TeamId != request.Context.TeamId)
        {
            throw new BusinessException("该应用不属于 token 归属团队.") { StatusCode = 403 };
        }

        if (app!.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持对话.") { StatusCode = 400 };
        }

        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = app.TeamId,
            AppId = app.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle : request.Title!,
            UserType = (int)sessionUserType,
            LastMessageTime = DateTimeOffset.Now,
            CreateUserId = identityId,
        };

        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 使用次数 +1（Redis 计数器，删除会话不回退）
        await _appUsageCounter.IncrementAsync(app.Id, cancellationToken);

        return new SimpleGuid { Value = session.Id };
    }
}
