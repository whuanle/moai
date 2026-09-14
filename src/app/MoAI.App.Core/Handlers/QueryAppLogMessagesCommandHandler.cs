using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppLogMessagesCommand"/>
/// </summary>
public class QueryAppLogMessagesCommandHandler : IRequestHandler<QueryAppLogMessagesCommand, QueryAppLogMessagesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppLogMessagesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppLogMessagesCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppLogMessagesCommandResponse> Handle(QueryAppLogMessagesCommand request, CancellationToken cancellationToken)
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

        var session = await _databaseContext.AppAgentSessions
            .Where(x => x.Id == request.SessionId && x.AppId == request.AppId)
            .Select(x => new { x.Id, x.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        var rows = await _databaseContext.AppAgentMessages
            .Where(x => x.SessionId == request.SessionId)
            .OrderBy(x => x.Seq)
            .Select(x => new
            {
                x.Id,
                x.Seq,
                x.Role,
                x.Content,
                x.ToolCalls,
                x.ToolCallId,
                x.Reasoning,
                x.CompletionsId,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AppMessageItem
            {
                MessageId = x.Id,
                Seq = x.Seq,
                Role = x.Role,
                Content = x.Content,
                ToolCalls = x.ToolCalls,
                ToolCallId = x.ToolCallId,
                Reasoning = x.Reasoning,
                CompletionsId = x.CompletionsId,
                CreateTime = x.CreateTime
            })
            .ToList();

        return new QueryAppLogMessagesCommandResponse
        {
            SessionId = session.Id,
            Title = session.Title,
            Items = items
        };
    }
}
