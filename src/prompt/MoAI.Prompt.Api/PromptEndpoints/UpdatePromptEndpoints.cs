using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Prompt.Queries;
using MoAIPrompt.Api;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 修改提示词.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_prompt")]
public class UpdatePromptEndpoint : Endpoint<UpdatePromptCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdatePromptEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdatePromptCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryPromptCreateUserCommand
        {
            PromptId = req.PromptId
        });

        if (isCreator.UserId != _userContext.UserId)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
