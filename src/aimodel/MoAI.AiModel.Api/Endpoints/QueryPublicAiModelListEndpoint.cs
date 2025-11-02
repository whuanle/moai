using FastEndpoints;
using MediatR;
using MoAI.AiModel.Queries;
using MoAI.AiModel.Queries.Respones;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Endpoints;

/// <summary>
/// 获取 AI 模型列表型.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/public_modellist")]
public class QueryPublicAiModelListEndpoint : Endpoint<QueryUserViewAiModelListCommand, QueryUserViewAiModelListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicAiModelListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPublicAiModelListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryUserViewAiModelListCommandResponse> ExecuteAsync(QueryUserViewAiModelListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
