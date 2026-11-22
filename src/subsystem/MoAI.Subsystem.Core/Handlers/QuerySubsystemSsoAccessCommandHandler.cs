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

/// <inheritdoc cref="QuerySubsystemSsoAccessCommand"/>
public class QuerySubsystemSsoAccessCommandHandler : IRequestHandler<QuerySubsystemSsoAccessCommand, QuerySubsystemSsoAccessCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsystemSsoAccessCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public QuerySubsystemSsoAccessCommandHandler(DatabaseContext databaseContext, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async ValueTask<QuerySubsystemSsoAccessCommandResponse> Handle(QuerySubsystemSsoAccessCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System && x.AppType == AccessAppType.Subsystem)
            .FirstOrDefaultAsync(x => x.Key == request.SubsystemKey, cancellationToken);

        // 不存在、停用与未配置回调统一按不可用返回：匿名可调用，避免枚举探测子系统状态
        if (subsystem == null || subsystem.Status != AccessAppStatus.Enabled)
        {
            return Unavailable();
        }

        var callbacks = SubsystemCallbacks.Parse(subsystem.CallbackUrls);
        if (callbacks.Count == 0)
        {
            return Unavailable();
        }

        if (!string.IsNullOrWhiteSpace(request.RedirectUri)
            && !callbacks.Any(x => string.Equals(x, request.RedirectUri, StringComparison.Ordinal)))
        {
            return new QuerySubsystemSsoAccessCommandResponse
            {
                Allowed = false,
                Name = subsystem.Name,
                Reason = QuerySubsystemSsoAccessCommandResponse.ReasonCallbackMismatch,
            };
        }

        // 指定团队范围且既无团队授权也无组织授权：任何人都无法使用，直接拦截避免无意义登录
        if (subsystem.TeamScope == SubsystemTeamScope.SpecifiedTeams)
        {
            var hasTeamGrant = await _databaseContext.SubsystemTeamAuths
                .AnyAsync(x => x.SubsystemId == subsystem.Id, cancellationToken);
            var orgGrants = await _orgGrantService.GetGrantsAsync(OrgGrantResourceType.Subsystem, subsystem.Id, cancellationToken);
            if (!hasTeamGrant && orgGrants.Count == 0)
            {
                return new QuerySubsystemSsoAccessCommandResponse
                {
                    Allowed = false,
                    Name = subsystem.Name,
                    Reason = QuerySubsystemSsoAccessCommandResponse.ReasonNoGrants,
                };
            }
        }

        // 匿名访问：系统级可用则先去登录，登录后再按用户维度判定
        if (request.ContextUserId <= 0)
        {
            return new QuerySubsystemSsoAccessCommandResponse
            {
                Allowed = true,
                RequiresLogin = true,
                Name = subsystem.Name,
                Reason = QuerySubsystemSsoAccessCommandResponse.ReasonLoginRequired,
            };
        }

        var allowed = await SubsystemSsoAccessRules.IsUserAllowedAsync(_databaseContext, _orgGrantService, subsystem, request.ContextUserId, cancellationToken);
        if (!allowed)
        {
            return new QuerySubsystemSsoAccessCommandResponse
            {
                Allowed = false,
                Name = subsystem.Name,
                Reason = QuerySubsystemSsoAccessCommandResponse.ReasonNotAuthorized,
            };
        }

        return new QuerySubsystemSsoAccessCommandResponse
        {
            Allowed = true,
            Name = subsystem.Name,
            Reason = QuerySubsystemSsoAccessCommandResponse.ReasonOk,
        };
    }

    private static QuerySubsystemSsoAccessCommandResponse Unavailable()
    {
        return new QuerySubsystemSsoAccessCommandResponse
        {
            Allowed = false,
            Reason = QuerySubsystemSsoAccessCommandResponse.ReasonUnavailable,
        };
    }
}
