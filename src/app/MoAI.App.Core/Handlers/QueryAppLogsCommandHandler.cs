using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppLogsCommand"/>
/// </summary>
public class QueryAppLogsCommandHandler : IRequestHandler<QueryAppLogsCommand, QueryAppLogsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppLogsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryAppLogsCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppLogsCommandResponse> Handle(QueryAppLogsCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能查看日志.") { StatusCode = 403 };
        }

        var query = _databaseContext.AppAgentSessions.Where(x => x.AppId == app.Id);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Title.Contains(keyword));
        }

        if (request.UserType != null)
        {
            var userType = (int)request.UserType.Value;
            query = query.Where(x => x.UserType == userType);
        }

        if (request.From != null)
        {
            query = query.Where(x => x.LastMessageTime >= request.From);
        }

        if (request.To != null)
        {
            query = query.Where(x => x.LastMessageTime <= request.To);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.LastMessageTime)
            .ThenByDescending(x => x.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.UserType,
                x.InputTokens,
                x.OutTokens,
                x.TotalTokens,
                x.LastMessageTime,
                x.CreateUserId,
                x.CreateTime,
                x.UpdateUserId,
                x.UpdateTime
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AppLogItem
            {
                SessionId = x.Id,
                Title = x.Title,
                UserType = (UserType)x.UserType,
                OwnerId = x.CreateUserId,
                InputTokens = x.InputTokens,
                OutTokens = x.OutTokens,
                TotalTokens = x.TotalTokens,
                LastMessageTime = x.LastMessageTime,
                CreateUserId = (int)x.CreateUserId,
                CreateTime = x.CreateTime,
                UpdateUserId = (int)x.UpdateUserId,
                UpdateTime = x.UpdateTime
            })
            .ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);

        // 外部用户 id 不属于内部用户体系，填充结果不可用，显式清空
        foreach (var item in items.Where(x => x.UserType != UserType.Normal))
        {
            item.CreateUserName = string.Empty;
            item.UpdateUserName = string.Empty;
        }

        return new QueryAppLogsCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }
}
