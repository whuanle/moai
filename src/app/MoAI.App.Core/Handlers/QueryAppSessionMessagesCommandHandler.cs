using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppSessionMessagesCommand"/>
/// </summary>
public class QueryAppSessionMessagesCommandHandler : IRequestHandler<QueryAppSessionMessagesCommand, QueryAppSessionMessagesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppSessionMessagesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="systemOptions">系统配置（派生 hash 策略加盐文本）.</param>
    public QueryAppSessionMessagesCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSessionMessagesCommandResponse> Handle(QueryAppSessionMessagesCommand request, CancellationToken cancellationToken)
    {
        var session = await _databaseContext.AppAgentSessions
            .Where(x => _databaseContext.Apps.Any(app => app.Id == x.AppId))
            .Where(x => x.Id == request.SessionId)
            .Select(x => new { x.Id, x.Title, x.CreateUserId, x.AppId })
            .FirstOrDefaultAsync(cancellationToken);

        // 非归属用户按不存在处理，避免泄露会话存在性
        if (session == null || session.CreateUserId != request.ContextUserId)
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

        // 读侧脱敏兜底：覆盖启用安全策略之前的存量消息
        var policy = await AppMessageSecurityMasker.LoadPolicyAsync(_databaseContext, _systemOptions, session.AppId, cancellationToken);
        AppMessageSecurityMasker.Mask(policy, items);

        return new QueryAppSessionMessagesCommandResponse
        {
            SessionId = session.Id,
            Title = session.Title,
            Items = items
        };
    }
}
