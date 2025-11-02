using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Queries;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 获取话题详细内容.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/chat_history")]
public class QueryAiAssistantChatHistoryEndpoint : Endpoint<QueryUserViewAiAssistantChatHistoryCommand, QueryAiAssistantChatHistoryCommandResponse>
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
    public override async Task<QueryAiAssistantChatHistoryCommandResponse> ExecuteAsync(QueryUserViewAiAssistantChatHistoryCommand req, CancellationToken ct)
    {
        var creatorId = await _mediator.Send(
            new QueryAiAssistantCreatorCommand
            {
                ChatId = req.ChatId
            },
            ct);

        if (creatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到对话") { StatusCode = 404 };
        }

        return await _mediator.Send(
            new QueryUserViewAiAssistantChatHistoryCommand
            {
                ChatId = req.ChatId
            },
            ct);
    }
}
