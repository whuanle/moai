using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Handlers;
using MoAI.App.AIAssistant.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 更新聊天参数.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_chat")]
public class UpdateAiAssistanChatConfigEndpoint : Endpoint<UpdateAiAssistanChatConfigCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAiAssistanChatConfigEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateAiAssistanChatConfigEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateAiAssistanChatConfigCommand req, CancellationToken ct)
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

        return await _mediator.Send(req, ct);
    }
}
