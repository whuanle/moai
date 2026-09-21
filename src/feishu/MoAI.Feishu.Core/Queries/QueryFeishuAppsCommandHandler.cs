using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Feishu.Queries.Responses;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.Feishu.Queries;

/// <summary>
/// <inheritdoc cref="QueryFeishuAppsCommand"/>
/// </summary>
public class QueryFeishuAppsCommandHandler : IRequestHandler<QueryFeishuAppsCommand, QueryFeishuAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly FeishuConnectionManager _connectionManager;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFeishuAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="connectionManager">飞书长连接管理器.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryFeishuAppsCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        FeishuConnectionManager connectionManager,
        IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _connectionManager = connectionManager;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryFeishuAppsCommandResponse> Handle(QueryFeishuAppsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.FeishuApps.Where(x => x.TeamId == request.TeamId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.AppId.Contains(keyword));
        }

        var rows = await query
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new
            {
                x.Id,
                x.TeamId,
                x.Name,
                x.Description,
                x.AppId,
                x.Domain,
                x.IsDisable,
                x.CreateTime,
                x.CreateUserId,
                x.UpdateTime,
                x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        // 一个飞书应用可绑定多个渠道（应用渠道独占 + 外部源等订阅型渠道一对多），
        // 列表项只展示一条代表绑定：优先应用渠道，便于前端按绑定过滤本应用渠道
        var feishuAppIds = rows.Select(x => x.Id).ToList();
        var bindings = await _databaseContext.FeishuAppBindings
            .Where(x => feishuAppIds.Contains(x.FeishuAppId))
            .Select(x => new { x.FeishuAppId, x.ChannelType, x.ChannelId, x.CreateTime })
            .ToListAsync(cancellationToken);

        var bindingMap = bindings
            .GroupBy(x => x.FeishuAppId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(b => b.ChannelType == (int)FeishuChannelType.App ? 0 : 1).First());

        var items = rows
            .Select(x =>
            {
                bindingMap.TryGetValue(x.Id, out var binding);
                return new FeishuAppItem
                {
                    FeishuAppId = x.Id,
                    TeamId = x.TeamId,
                    Name = x.Name,
                    Description = x.Description,
                    AppId = x.AppId,
                    Domain = x.Domain,
                    IsDisable = x.IsDisable,
                    IsOnline = !x.IsDisable && _connectionManager.IsOnline(x.Id),
                    BindChannelType = binding == null ? null : (FeishuChannelType)binding.ChannelType,
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

        return new QueryFeishuAppsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items
        };
    }
}
