using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsystemsCommand"/>
/// </summary>
public class QuerySubsystemsCommandHandler : IRequestHandler<QuerySubsystemsCommand, QuerySubsystemsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SubsystemOnlineService _onlineService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsystemsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QuerySubsystemsCommandHandler(DatabaseContext databaseContext, SubsystemOnlineService onlineService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsystemsCommandResponse> Handle(QuerySubsystemsCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Subsystems.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Key.Contains(search) || x.Name.Contains(search));
        }

        // 排序在内存侧进行，保持与 sqlite 单测兼容
        var subsystems = await query.ToListAsync(cancellationToken);
        subsystems.Sort(static (a, b) => b.CreateTime.CompareTo(a.CreateTime));

        var subsystemIds = subsystems.Select(x => x.Id).ToList();

        // 授权行一次拉全，团队名称按 id 批量补齐，避免逐子系统查询
        var authRows = await _databaseContext.SubsystemTeamAuths
            .Where(x => subsystemIds.Contains(x.SubsystemId))
            .Select(x => new { x.SubsystemId, x.TeamId })
            .ToListAsync(cancellationToken);
        var authTeamIds = authRows.Select(x => x.TeamId).Distinct().ToList();
        var teamNames = await _databaseContext.Teams
            .Where(x => authTeamIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var authsBySubsystem = authRows
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.TeamId).ToList());

        var onlineTasks = subsystems.Select(x => _onlineService.IsOnlineAsync(x.Id));
        var onlineFlags = await Task.WhenAll(onlineTasks);

        var items = subsystems.Select((x, index) =>
        {
            var auths = authsBySubsystem.GetValueOrDefault(x.Id) ?? [];
            return new QuerySubsystemsCommandResponseItem
            {
                SubsystemId = x.Id,
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status,
                TeamScope = x.TeamScope,
                AuthorizedTeamCount = auths.Count,
                AuthorizedTeams = auths.Select(a => new AuthorizedTeamItem
                {
                    TeamId = a.TeamId,
                    TeamName = teamNames.GetValueOrDefault(a.TeamId, $"#{a.TeamId}"),
                }).ToList(),
                Online = onlineFlags[index],
                CallbackUrl = x.CallbackUrl,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            };
        }).ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);
        return new QuerySubsystemsCommandResponse { Items = items };
    }
}
