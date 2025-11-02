using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 获取用户有权访问的知识库列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/query_wiki_list")]
public class QueryWikiBaseListEndpoint : Endpoint<QueryWikiBaseListCommand, IReadOnlyCollection<QueryWikiInfoResponse>>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiBaseListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiBaseListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyCollection<QueryWikiInfoResponse>> ExecuteAsync(QueryWikiBaseListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
