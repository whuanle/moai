using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Queries;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 删除对话.
/// </summary>
[HttpDelete($"{ApiPrefix.Prefix}/delete_chat")]
public class DeleteAiAssistantChatEndpoint : Endpoint<DeleteAiAssistantChatCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiAssistantChatEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeleteAiAssistantChatEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteAiAssistantChatCommand req, CancellationToken ct)
    {
        return await _mediator.Send(
            new DeleteAiAssistantChatCommand
            {
                UserId = _userContext.UserId,
                ChatId = req.ChatId
            },
            ct);
    }
}
