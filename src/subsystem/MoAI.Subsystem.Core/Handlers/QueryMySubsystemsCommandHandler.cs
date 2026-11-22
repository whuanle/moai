using Mediator;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Org.Services;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMySubsystemsCommand"/>
/// </summary>
public class QueryMySubsystemsCommandHandler : IRequestHandler<QueryMySubsystemsCommand, QueryMySubsystemsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMySubsystemsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public QueryMySubsystemsCommandHandler(DatabaseContext databaseContext, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async ValueTask<QueryMySubsystemsCommandResponse> Handle(QueryMySubsystemsCommand request, CancellationToken cancellationToken)
    {
        // 用户面门户仅服务登录用户；无用户上下文时返回空列表（兜底，正常由内部 JWT 鉴权拦截）
        if (request.ContextUserId <= 0)
        {
            return new QueryMySubsystemsCommandResponse();
        }

        // 排序在内存侧进行，保持与 sqlite 单测兼容（SQLite 不支持 DateTimeOffset ORDER BY）
        var subsystems = (await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System
                && x.AppType == AccessAppType.Subsystem
                && x.Status == AccessAppStatus.Enabled)
            .ToListAsync(cancellationToken))
            .OrderBy(x => x.CreateTime)
            .ToList();

        var items = new List<QueryMySubsystemsCommandResponseItem>();
        foreach (var subsystem in subsystems)
        {
            // 未配置回调地址=未开放委托登录，无法进入：不下发到门户
            if (SubsystemCallbacks.Parse(subsystem.CallbackUrls).Count == 0)
            {
                continue;
            }

            // 授权口径与 SSO 预检/签发一致（团队范围 ∪ 组织授权），避免门户可见但点进入被拒
            var allowed = await SubsystemSsoAccessRules.IsUserAllowedAsync(
                _databaseContext,
                _orgGrantService,
                subsystem,
                request.ContextUserId,
                cancellationToken);
            if (!allowed)
            {
                continue;
            }

            items.Add(new QueryMySubsystemsCommandResponseItem
            {
                SubsystemId = subsystem.Id,
                Key = subsystem.Key,
                Name = subsystem.Name,
                Description = subsystem.Description,
                AvatarPath = subsystem.AvatarPath,
            });
        }

        return new QueryMySubsystemsCommandResponse { Items = items };
    }
}
