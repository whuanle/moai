using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalKgRecallCommand"/>
/// 复用图检索消费层 GraphSearchService（向量召回 + 一跳扩展），仅托管图参与（接入图 409，与外部写接口语义一致）；
/// 图谱未配置向量化/模型不可用时结果为空并带 skippedHint 可读说明（不抛错，方便模型自行降级用节点搜索）.
/// </summary>
public class QueryExternalKgRecallCommandHandler : IRequestHandler<QueryExternalKgRecallCommand, QueryExternalKgRecallCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IGraphSearchService _graphSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalKgRecallCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="graphSearchService">图检索服务.</param>
    public QueryExternalKgRecallCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IGraphSearchService graphSearchService)
    {
        _externalAuthorizer = externalAuthorizer;
        _graphSearchService = graphSearchService;
    }

    /// <inheritdoc/>
    public async Task<QueryExternalKgRecallCommandResponse> Handle(QueryExternalKgRecallCommand request, CancellationToken cancellationToken)
    {
        var graph = await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("接入模式图谱不支持向量召回，可使用节点关键字搜索.") { StatusCode = 409 };
        }

        var result = await _graphSearchService.SearchAsync([request.KnowledgeGraphId], request.Query, request.Top, request.MinScore, cancellationToken);

        return new QueryExternalKgRecallCommandResponse
        {
            KnowledgeGraphId = request.KnowledgeGraphId,
            KnowledgeGraphName = graph.Name,
            Query = request.Query,
            Items = result.Hits.Select(x => new QueryExternalKgRecallItem
            {
                NodeId = x.NodeId,
                Name = x.Name,
                Description = x.Description,
                EntityTypeName = x.EntityTypeName,
                Score = x.Score,
                Neighbors = x.Neighbors.Select(n => new KgRecallNeighbor
                {
                    RelationName = n.RelationName,
                    Direction = n.Direction,
                    Name = n.Name,
                    Description = n.Description,
                }).ToList(),
            }).ToList(),
            SkippedHint = result.SkippedHints.Count > 0 ? string.Join('；', result.SkippedHints) : null,
        };
    }
}
