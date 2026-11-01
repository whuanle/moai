using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Services;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsysAppsCommand"/>
/// </summary>
public class QuerySubsysAppsCommandHandler : IRequestHandler<QuerySubsysAppsCommand, QuerySubsysAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemOnlineService _onlineService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsysAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QuerySubsysAppsCommandHandler(DatabaseContext databaseContext, ISubsystemOnlineService onlineService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsysAppsCommandResponse> Handle(QuerySubsysAppsCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.AccessApps.Where(x => x.Level == AccessAppLevel.System);
        if (request.AppType.HasValue)
        {
            query = query.Where(x => x.AppType == request.AppType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Key.Contains(search) || x.Name.Contains(search));
        }

        // 排序在内存侧进行，保持与 sqlite 单测兼容
        var apps = await query.ToListAsync(cancellationToken);
        apps.Sort(static (a, b) => b.CreateTime.CompareTo(a.CreateTime));

        var appIds = apps.Select(x => x.Id).ToList();

        // 子系统授权行一次拉全，团队名称按 id 批量补齐，避免逐行查询
        var authRows = await _databaseContext.SubsystemTeamAuths
            .Where(x => appIds.Contains(x.SubsystemId))
            .Select(x => new { x.SubsystemId, x.TeamId })
            .ToListAsync(cancellationToken);
        var authTeamIds = authRows.Select(x => x.TeamId).Distinct().ToList();
        var teamNames = await _databaseContext.Teams
            .Where(x => authTeamIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var authsByApp = authRows
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.TeamId).ToList());

        // 组织授权行一次拉全（统一组织授权表，资源主键为字符串形态的接入 id），角色名称按 id 批量补齐
        var appIdStrings = appIds.Select(x => x.ToString()).ToList();
        var orgGrantRows = await _databaseContext.OrgGrants
            .Where(x => x.ResourceType == (int)OrgGrantResourceType.Subsystem && appIdStrings.Contains(x.ResourceId))
            .Select(x => new { x.ResourceId, x.OrgRoleId, x.Scope })
            .ToListAsync(cancellationToken);
        var grantRoleIds = orgGrantRows.Select(x => x.OrgRoleId).Distinct().ToList();
        var orgRoleNames = await _databaseContext.OrgRoles
            .Where(x => grantRoleIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var orgGrantsByApp = orgGrantRows
            .GroupBy(x => x.ResourceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var onlineTasks = apps.Select(x => _onlineService.GetOnlineInstanceCountAsync(x.Id));
        var onlineCounts = await Task.WhenAll(onlineTasks);

        var items = apps.Select((x, index) =>
        {
            var auths = authsByApp.GetValueOrDefault(x.Id) ?? [];
            var isSubsystem = x.AppType == AccessAppType.Subsystem;
            return new QuerySubsysAppsCommandResponseItem
            {
                Id = x.Id,
                AppType = x.AppType,
                // 应用接入 appid 为直连凭证，仅回显前缀；子系统 appid 为自填标识可全量展示
                Key = isSubsystem ? x.Key : string.Empty,
                KeyPrefix = x.KeyPrefix,
                SecretMask = x.SecretMask,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status,
                TeamScope = x.TeamScope,
                AuthorizedTeamCount = auths.Count,
                AuthorizedTeams = isSubsystem
                    ? auths.Select(a => new AuthorizedTeamItem
                    {
                        TeamId = a.TeamId,
                        TeamName = teamNames.GetValueOrDefault(a.TeamId, $"#{a.TeamId}"),
                    }).ToList()
                    : [],
                AuthorizedOrgs = isSubsystem
                    ? (orgGrantsByApp.GetValueOrDefault(x.Id.ToString()) ?? [])
                        .OrderBy(o => o.OrgRoleId)
                        .Select(o => new AuthorizedOrgItem
                        {
                            OrgRoleId = o.OrgRoleId,
                            OrgName = orgRoleNames.GetValueOrDefault(o.OrgRoleId, $"#{o.OrgRoleId}"),
                            Scope = (OrgGrantScope)o.Scope,
                        }).ToList()
                    : [],
                Online = onlineCounts[index] > 0,
                OnlineInstances = onlineCounts[index],
                CallbackUrls = SubsystemCallbacks.Parse(x.CallbackUrls),
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            };
        }).ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QuerySubsysAppsCommandResponse { Items = items };
    }
}

/// <summary>
/// <inheritdoc cref="QuerySubsysAppInfoCommand"/>
/// </summary>
public class QuerySubsysAppInfoCommandHandler : IRequestHandler<QuerySubsysAppInfoCommand, QuerySubsysAppInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsysAppInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    public QuerySubsysAppInfoCommandHandler(DatabaseContext databaseContext, ISubsystemOnlineService onlineService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsysAppInfoCommandResponse> Handle(QuerySubsysAppInfoCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System)
            .FirstOrDefaultAsync(x => x.Id == request.SubsysAppId, cancellationToken)
            ?? throw new BusinessException("接入不存在.") { StatusCode = 404 };

        var authorizedTeamCount = await _databaseContext.SubsystemTeamAuths
            .CountAsync(x => x.SubsystemId == app.Id, cancellationToken);
        var onlineInstances = await _onlineService.GetOnlineInstanceCountAsync(app.Id);

        return new QuerySubsysAppInfoCommandResponse
        {
            Id = app.Id,
            Key = app.Key,
            Name = app.Name,
            AppType = app.AppType,
            Status = app.Status,
            TeamScope = app.TeamScope,
            AuthorizedTeamCount = authorizedTeamCount,
            CallbackUrls = SubsystemCallbacks.Parse(app.CallbackUrls),
            Online = onlineInstances > 0,
            OnlineInstances = onlineInstances,
        };
    }
}
