using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphSearchCommand"/>
/// </summary>
public class QueryKnowledgeGraphSearchCommandHandler : IRequestHandler<QueryKnowledgeGraphSearchCommand, QueryKnowledgeGraphSearchCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IGraphSearchService _graphSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphSearchCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="graphSearchService">图检索服务.</param>
    public QueryKnowledgeGraphSearchCommandHandler(IKnowledgeGraphAuthorizer authorizer, IGraphSearchService graphSearchService)
    {
        _authorizer = authorizer;
        _graphSearchService = graphSearchService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphSearchCommandResponse> Handle(QueryKnowledgeGraphSearchCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持向量检索.") { StatusCode = 409 };
        }

        // Guid? 判空用 == null，禁止 == Guid.Empty 哨兵（对齐 GraphSearchService）
        if (graph.EmbeddingModelId == null || graph.EmbeddingDimensions <= 0)
        {
            throw new BusinessException("该图谱未配置向量化模型，请先在图谱设置中配置.") { StatusCode = 409 };
        }

        var result = await _graphSearchService.SearchAsync([graph.Id], request.Query, request.TopK, request.MinScore, cancellationToken);

        return new QueryKnowledgeGraphSearchCommandResponse
        {
            Hits = result.Hits.Select(x => new QueryKnowledgeGraphSearchItem
            {
                KgId = x.KgId,
                NodeId = x.NodeId,
                Name = x.Name,
                Description = x.Description,
                EntityTypeId = x.EntityTypeId,
                EntityTypeName = x.EntityTypeName,
                Score = x.Score,
                Neighbors = x.Neighbors.Select(n => new QueryKnowledgeGraphSearchNeighborItem
                {
                    RelationName = n.RelationName,
                    Direction = n.Direction,
                    Name = n.Name,
                    Description = n.Description,
                }).ToList(),
            }).ToList(),
            Contents = result.Contents.ToList(),
            Text = result.Text,
            SkippedHints = result.SkippedHints.ToList(),
        };
    }
}
