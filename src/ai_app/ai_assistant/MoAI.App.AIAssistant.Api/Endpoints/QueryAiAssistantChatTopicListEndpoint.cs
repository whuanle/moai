using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Queries;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 获取用户所有话题记录.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/topic_list")]
public class QueryAiAssistantChatTopicListEndpoint : EndpointWithoutRequest<QueryAiAssistantChatTopicListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatTopicListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryAiAssistantChatTopicListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAiAssistantChatTopicListCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(
            new QueryAiAssistantChatTopicListCommand
            {
                UserId = _userContext.UserId,
            },
            ct);
    }
}