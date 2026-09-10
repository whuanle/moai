using MediatR;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphNodesCommand"/>
/// </summary>
public class QueryKnowledgeGraphNodesCommandHandler : IRequestHandler<QueryKnowledgeGraphNodesCommand, QueryKnowledgeGraphNodesCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphNodesCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphNodesCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphNodesCommandResponse> Handle(QueryKnowledgeGraphNodesCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await _store.ListNodesAsync(request.KgId, request.EntityTypeId, request.Keyword, pageNo, pageSize, cancellationToken);
        return new QueryKnowledgeGraphNodesCommandResponse
        {
            Total = total,
            Items = items.Select(x => new KnowledgeGraphNodeItem
            {
                NodeId = x.Id,
                EntityTypeId = x.EntityTypeId,
                Name = x.Name,
                Description = x.Description,
            }).ToList(),
        };
    }
}
