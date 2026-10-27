using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.WeixinWork.Queries;
using MoAI.WeixinWork.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.WeixinWork.Queries;

/// <summary>
/// <inheritdoc cref="QueryWeixinWorkAppsCommand"/>
/// </summary>
public class QueryWeixinWorkAppsCommandHandler : IRequestHandler<QueryWeixinWorkAppsCommand, QueryWeixinWorkAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWeixinWorkAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    public QueryWeixinWorkAppsCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryWeixinWorkAppsCommandResponse> Handle(QueryWeixinWorkAppsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var query = _databaseContext.WeixinWorkApps.Where(x => x.TeamId == request.TeamId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.CorpId.Contains(keyword));
        }

        var rows = await query
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new
            {
                x.Id,
                x.TeamId,
                x.Name,
                x.Description,
                x.CorpId,
                x.AgentId,
                x.ApiEndpoint,
                x.IsDisable,
                x.CreateTime,
                x.CreateUserId,
                x.UpdateTime,
                x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        // 一个企业微信应用可绑定多个渠道（当前应用渠道独占），
        // 列表项只展示一条代表绑定：优先应用渠道，便于前端按绑定过滤本应用渠道
        var weixinWorkAppIds = rows.Select(x => x.Id).ToList();
        var bindings = await _databaseContext.WeixinWorkAppBindings
            .Where(x => weixinWorkAppIds.Contains(x.WeixinWorkAppId))
            .Select(x => new { x.WeixinWorkAppId, x.ChannelType, x.ChannelId, x.CreateTime })
            .ToListAsync(cancellationToken);

        var bindingMap = bindings
            .GroupBy(x => x.WeixinWorkAppId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(b => b.ChannelType == (int)WeixinWorkChannelType.App ? 0 : 1).First());

        var items = rows
            .Select(x =>
            {
                bindingMap.TryGetValue(x.Id, out var binding);
                return new WeixinWorkAppItem
                {
                    WeixinWorkAppId = x.Id,
                    TeamId = x.TeamId,
                    Name = x.Name,
                    Description = x.Description,
                    CorpId = x.CorpId,
                    AgentId = x.AgentId,
                    ApiEndpoint = x.ApiEndpoint,
                    IsDisable = x.IsDisable,
                    IsOnline = !x.IsDisable,
                    BindChannelType = binding == null ? null : (WeixinWorkChannelType)binding.ChannelType,
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

        return new QueryWeixinWorkAppsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items
        };
    }
}
