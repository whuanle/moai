using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Org.Services;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Services;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SubsystemSsoAuthorizeCommand"/>
/// </summary>
public class SubsystemSsoAuthorizeCommandHandler : IRequestHandler<SubsystemSsoAuthorizeCommand, SubsystemSsoAuthorizeCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemSsoCodeStore _codeStore;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSsoAuthorizeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="codeStore">委托码存储.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public SubsystemSsoAuthorizeCommandHandler(DatabaseContext databaseContext, ISubsystemSsoCodeStore codeStore, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _codeStore = codeStore;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<SubsystemSsoAuthorizeCommandResponse> Handle(SubsystemSsoAuthorizeCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System && x.AppType == AccessAppType.Subsystem)
            .FirstOrDefaultAsync(x => x.Key == request.SubsystemKey, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        if (subsystem.Status == AccessAppStatus.Disabled)
        {
            throw new BusinessException("子系统已停用.") { StatusCode = 403 };
        }

        // 回调地址白名单：子系统可注册多个入口（测试/生产等）；redirect_uri 传入时须精确命中，缺省取注册列表第一个
        var callbacks = SubsystemCallbacks.Parse(subsystem.CallbackUrls);
        if (callbacks.Count == 0)
        {
            throw new BusinessException("该子系统未配置登录回调地址，暂不支持委托登录.") { StatusCode = 403 };
        }

        string callback;
        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            callback = callbacks[0];
        }
        else
        {
            callback = callbacks.FirstOrDefault(x => string.Equals(x, request.RedirectUri, StringComparison.Ordinal))
                ?? throw new BusinessException("回调地址未在子系统注册列表内.") { StatusCode = 403 };
        }

        // 授权门禁：团队维度与组织维度任一命中即放通（并集）
        // 团队维度：全员范围要求用户至少加入一个团队；指定范围要求用户团队与授权清单有交集
        var userTeamIds = await _databaseContext.TeamUsers
            .Where(x => x.UserId == request.ContextUserId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);
        bool teamHit;
        if (subsystem.TeamScope == SubsystemTeamScope.SpecifiedTeams)
        {
            var authorizedTeamIds = await _databaseContext.SubsystemTeamAuths
                .Where(x => x.SubsystemId == subsystem.Id)
                .Select(x => x.TeamId)
                .ToListAsync(cancellationToken);
            teamHit = userTeamIds.Intersect(authorizedTeamIds).Any();
        }
        else
        {
            teamHit = userTeamIds.Count > 0;
        }

        // 组织维度：统一组织授权表（org_grant，资源类型 subsystem）按角色方向规则（self/up/down）判定
        var orgHit = await _orgGrantService.IsAllowedAsync(OrgGrantResourceType.Subsystem, subsystem.Id, request.ContextUserId, cancellationToken);
        if (!teamHit && !orgHit)
        {
            throw new BusinessException(subsystem.TeamScope == SubsystemTeamScope.SpecifiedTeams
                ? "你所在的团队或组织未获授权使用该子系统."
                : "你尚未加入任何团队，也未获得组织授权，无法使用该子系统.") { StatusCode = 403 };
        }

        var code = await _codeStore.CreateAsync(subsystem.Id, request.ContextUserId);
        return new SubsystemSsoAuthorizeCommandResponse
        {
            Code = code,
            CallbackUrl = callback,
            Name = subsystem.Name,
        };
    }
}
