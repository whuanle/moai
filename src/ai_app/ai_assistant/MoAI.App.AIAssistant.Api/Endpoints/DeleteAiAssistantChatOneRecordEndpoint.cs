using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 删除对话中的一条记录.
/// </summary>
[HttpDelete($"{ApiPrefix.Prefix}/delete_chat_record")]
public class DeleteAiAssistantChatOneRecordEndpoint : Endpoint<DeleteAiAssistantChatOneRecordCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiAssistantChatOneRecordEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeleteAiAssistantChatOneRecordEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteAiAssistantChatOneRecordCommand req, CancellationToken ct)
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
            new DeleteAiAssistantChatOneRecordCommand
            {
                ChatId = req.ChatId,
                RecordId = req.RecordId
            },
            ct);
    }
}
