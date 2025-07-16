using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Queries;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 获取话题详细内容.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/chat_history")]
public class QueryAiAssistantChatHistoryEndpoint : Endpoint<QueryAiAssistantChatHistoryCommand, QueryAiAssistantChatHistoryCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatHistoryEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryAiAssistantChatHistoryEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAiAssistantChatHistoryCommandResponse> ExecuteAsync(QueryAiAssistantChatHistoryCommand req, CancellationToken ct)
    {
        return await _mediator.Send(
            new QueryAiAssistantChatHistoryCommand
            {
                UserId = _userContext.UserId,
                ChatId = req.ChatId
            },
            ct);
    }
}
