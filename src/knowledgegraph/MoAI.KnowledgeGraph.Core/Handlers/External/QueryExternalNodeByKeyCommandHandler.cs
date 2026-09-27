using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalNodeByKeyCommand"/>
/// 按业务 key 查询节点详情，用于导入后同步核对；key 不存在返回 404.
/// </summary>
public class QueryExternalNodeByKeyCommandHandler : IRequestHandler<QueryExternalNodeByKeyCommand, QueryKnowledgeGraphNodeCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalNodeByKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalNodeByKeyCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphNodeCommandResponse> Handle(QueryExternalNodeByKeyCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        var node = await _store.GetNodeByKeyAsync(request.KnowledgeGraphId, request.Key, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        return new QueryKnowledgeGraphNodeCommandResponse
        {
            NodeId = node.Id,
            EntityTypeId = node.EntityTypeId,
            Name = node.Name,
            Description = node.Description,
            Properties = KnowledgeGraphPropertyJson.ParseValues(node.PropsJson),
        };
    }
}
