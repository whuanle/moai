using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppSessionsCommand"/>
/// </summary>
public class QueryAppSessionsCommandHandler : IRequestHandler<QueryAppSessionsCommand, QueryAppSessionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppSessionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppSessionsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSessionsCommandResponse> Handle(QueryAppSessionsCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId, x.IsExternal, x.IsPublic, x.PublishStatus })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null || app.IsExternal)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        // 非团队成员仅在应用「已发布且公开到平台」时可查看自己的会话
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var rows = await _databaseContext.AppAgentSessions
            .Where(x => x.AppId == request.AppId && x.CreateUserId == request.ContextUserId)
            .OrderByDescending(x => x.LastMessageTime)
            .Select(x => new
            {
                x.Id,
                x.AppId,
                x.Title,
                x.PromptId,
                x.UserType,
                x.InputTokens,
                x.OutTokens,
                x.TotalTokens,
                x.LastMessageTime,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AppSessionItem
            {
                SessionId = x.Id,
                AppId = x.AppId,
                Title = x.Title,
                PromptId = x.PromptId,
                UserType = x.UserType,
                InputTokens = x.InputTokens,
                OutTokens = x.OutTokens,
                TotalTokens = x.TotalTokens,
                LastMessageTime = x.LastMessageTime,
                CreateTime = x.CreateTime
            })
            .ToList();

        return new QueryAppSessionsCommandResponse
        {
            AppId = request.AppId,
            MyRole = myRole == null ? -1 : (int)myRole.Value,
            Items = items
        };
    }
}
