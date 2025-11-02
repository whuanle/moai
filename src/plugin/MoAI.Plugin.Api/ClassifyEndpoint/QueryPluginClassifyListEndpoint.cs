using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.ClassifyEndpoint;

/// <summary>
/// 获取分类列表.
/// </summary>
[HttpGet($"{ApiPrefix.AdminPrefix}/classify_list")]
public class QueryPluginClassifyListEndpoint : EndpointWithoutRequest<QueryPluginClassifyListCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginClassifyListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public QueryPluginClassifyListEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginClassifyListCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(new QueryPluginClassifyListCommand(), ct);
    }
}
