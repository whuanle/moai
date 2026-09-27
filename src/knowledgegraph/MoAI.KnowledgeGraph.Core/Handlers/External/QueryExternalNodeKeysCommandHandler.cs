using MediatR;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalNodeKeysCommand"/>
/// 分页枚举图内已落业务 key 的节点，用于外部同步场景全量比对（找出源数据已移除、图中仍存在的 key）.
/// </summary>
public class QueryExternalNodeKeysCommandHandler : IRequestHandler<QueryExternalNodeKeysCommand, QueryExternalNodeKeysResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalNodeKeysCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalNodeKeysCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalNodeKeysResponse> Handle(QueryExternalNodeKeysCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        var (items, _) = await _store.ListNodeKeysAsync(request.KnowledgeGraphId, request.PageNo, request.PageSize, cancellationToken);
        return new QueryExternalNodeKeysResponse
        {
            Items = items.Select(x => new ExternalNodeKeyItem
            {
                Key = x.Key,
                NodeId = x.Id,
                Name = x.Name,
                EntityTypeId = x.EntityTypeId,
            }).ToList(),
        };
    }
}
