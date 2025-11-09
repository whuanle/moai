using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.PromptEndpoints.Models;
using MoAIPrompt.Api;
using MoAIPrompt.Models;
using MoAIPrompt.Queries;

namespace MoAI.Prompt.PromptEndpoints;

/// <summary>
/// 获取提示词内容.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/prompt_content")]
public class QueryPromptEndpoint : Endpoint<QueryPromptContentRequest, PromptItem>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPromptEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<PromptItem> ExecuteAsync(QueryPromptContentRequest req, CancellationToken ct)
    {
        var newReq = new QueryPromptCommand
        {
            PromptId = req.PromptId,
            UserId = _userContext.UserId
        };

        return await _mediator.Send(newReq, ct);
    }
}
