using FastEndpoints;
using MediatR;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;
using MoAIChat.Core.Handlers;

namespace MoAI.App.AIAssistant.Endpoints;

/// <summary>
/// 发起新的聊天，检查用户是否有知识库、插件灯权限，如果检查通过，返回聊天 id.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/create_chat")]
public class CreateAiAssistantChatEndpoint : Endpoint<AIAssistantChatObject, CreateAiAssistantChatCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAiAssistantChatEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public CreateAiAssistantChatEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<CreateAiAssistantChatCommandResponse> ExecuteAsync(AIAssistantChatObject req, CancellationToken ct)
    {
        var command = new CreateAiAssistantChatCommand
        {
            ExecutionSettings = req.ExecutionSettings,
            ModelId = req.ModelId,
            PluginIds = req.PluginIds,
            Prompt = req.Prompt,
            Title = req.Title,
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        };

        return await _mediator.Send(command, ct);
    }
}
