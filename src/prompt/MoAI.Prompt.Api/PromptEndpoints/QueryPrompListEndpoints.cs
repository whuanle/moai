using FastEndpoints;
using MediatR;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAIPrompt.Api;
using MoAIPrompt.Queries;
using MoAIPrompt.Queries.Responses;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 查询提示词.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/prompt_list")]
public class QueryPrompListEndpoint : Endpoint<QueryPromptListCommand, QueryPromptListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPrompListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPrompListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPromptListCommandResponse> ExecuteAsync(QueryPromptListCommand req, CancellationToken ct)
    {
        req.SetProperty(x => x.ContextUserId, _userContext.UserId);
        return await _mediator.Send(req, ct);
    }
}
