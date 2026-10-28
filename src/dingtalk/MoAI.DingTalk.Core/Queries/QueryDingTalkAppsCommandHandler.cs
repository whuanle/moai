using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.DingTalk.Queries.Responses;
using MoAI.DingTalk.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.DingTalk.Queries;

/// <summary>
/// <inheritdoc cref="QueryDingTalkAppsCommand"/>
/// </summary>
public class QueryDingTalkAppsCommandHandler : IRequestHandler<QueryDingTalkAppsCommand, QueryDingTalkAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DingTalkConnectionManager _connectionManager;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryDingTalkAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">钉钉长连接管理器.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryDingTalkAppsCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        DingTalkConnectionManager connectionManager,
        IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryDingTalkAppsCommandResponse> Handle(QueryDingTalkAppsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.DingTalkApps.Where(x => x.TeamId == request.TeamId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.AppKey.Contains(keyword));
        }

        var rows = await query
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new
            {
                x.Id,
                x.TeamId,
                x.Name,
                x.Description,
                x.AppKey,
                x.IsDisable,
                x.CreateTime,
                x.CreateUserId,
                x.UpdateTime,
                x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        // 一个钉钉应用可绑定多个渠道（当前应用渠道独占），
        // 列表项只展示一条代表绑定：优先应用渠道，便于前端按绑定过滤本应用渠道
        var dingTalkAppIds = rows.Select(x => x.Id).ToList();
        var bindings = await _databaseContext.DingTalkAppBindings
            .Where(x => dingTalkAppIds.Contains(x.DingTalkAppId))
            .Select(x => new { x.DingTalkAppId, x.ChannelType, x.ChannelId, x.CreateTime })
            .ToListAsync(cancellationToken);

        var bindingMap = bindings
            .GroupBy(x => x.DingTalkAppId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(b => b.ChannelType == (int)DingTalkChannelType.App ? 0 : 1).First());

        var items = rows
            .Select(x =>
            {
                bindingMap.TryGetValue(x.Id, out var binding);
                return new DingTalkAppItem
                {
                    DingTalkAppId = x.Id,
                    TeamId = x.TeamId,
                    Name = x.Name,
                    Description = x.Description,
                    AppKey = x.AppKey,
                    IsDisable = x.IsDisable,
                    IsOnline = !x.IsDisable && _connectionManager.IsOnline(x.Id),
                    BindChannelType = binding == null ? null : (DingTalkChannelType)binding.ChannelType,
                    BindChannelId = binding?.ChannelId,
                    BindTime = binding?.CreateTime,
                    CreateTime = x.CreateTime,
                    CreateUserId = (int)x.CreateUserId,
                    UpdateTime = x.UpdateTime,
                    UpdateUserId = (int)x.UpdateUserId,
                };
            })
            .ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryDingTalkAppsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items
        };
    }
}
