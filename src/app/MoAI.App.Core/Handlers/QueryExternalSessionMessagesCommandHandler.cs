using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalSessionMessagesCommand"/>
/// </summary>
public class QueryExternalSessionMessagesCommandHandler : IRequestHandler<QueryExternalSessionMessagesCommand, QueryAppSessionMessagesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalSessionMessagesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryExternalSessionMessagesCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSessionMessagesCommandResponse> Handle(QueryExternalSessionMessagesCommand request, CancellationToken cancellationToken)
    {
        var externalUserId = ExternalAppAccessValidator.EnsureExternalUser(request.Context);

        var session = await _databaseContext.AppAgentSessions
            .Where(x => x.Id == request.SessionId)
            .Select(x => new { x.Id, x.Title, x.AppId, x.CreateUserId })
            .FirstOrDefaultAsync(cancellationToken);

        // 非归属用户按不存在处理，避免泄露会话存在性；应用不在授权范围同样按不存在处理
        if (session == null || session.CreateUserId != externalUserId || !request.Context.IsAppAuthorized(session.AppId))
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        var rows = await _databaseContext.AppAgentMessages
            .Where(x => x.SessionId == session.Id)
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

        return new QueryAppSessionMessagesCommandResponse
        {
            SessionId = session.Id,
            Title = session.Title,
            Items = items
        };
    }
}
