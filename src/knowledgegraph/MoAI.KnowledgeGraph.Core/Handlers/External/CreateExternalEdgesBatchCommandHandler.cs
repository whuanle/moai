using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalEdgesBatchCommand"/>
/// </summary>
public class CreateExternalEdgesBatchCommandHandler : IRequestHandler<CreateExternalEdgesBatchCommand, ExternalBatchResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalEdgesBatchCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public CreateExternalEdgesBatchCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ExternalBatchResponse> Handle(CreateExternalEdgesBatchCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);

        // 逐条业务校验，任一失败整批拒绝（不触发图库写入）.
        var relationTypeIds = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var relationTypeIdSet = relationTypeIds.ToHashSet();

        // 端点预检：分页拉取该图谱全部节点 id 集合，一次集合判断完成端点存在校验.
        var nodeIds = await ListAllNodeIdsAsync(request.KnowledgeGraphId, cancellationToken);
        var nodeIdSet = nodeIds.ToHashSet(StringComparer.Ordinal);

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (!relationTypeIdSet.Contains(item.RelationTypeId))
            {
                throw new BusinessException($"第 {index} 条校验失败：关系类型不存在.") { StatusCode = 400 };
            }

            if (!nodeIdSet.Contains(item.SourceNodeId))
            {
                throw new BusinessException($"第 {index} 条校验失败：起点节点不存在.") { StatusCode = 400 };
            }

            if (!nodeIdSet.Contains(item.TargetNodeId))
            {
                throw new BusinessException($"第 {index} 条校验失败：终点节点不存在.") { StatusCode = 400 };
            }
        }

        var edges = request.Items
            .Select(x => new KnowledgeGraphEdgeInput(x.RelationTypeId, x.SourceNodeId, x.TargetNodeId))
            .ToList();
        var records = await _store.CreateEdgesBatchAsync(request.KnowledgeGraphId, edges, cancellationToken);

        return new ExternalBatchResponse
        {
            SuccessCount = records.Count,
            FailedCount = 0,
            Results = records.Select((record, index) => new ExternalBatchItemResult
            {
                Index = index,
                Ok = true,
                Id = record.Id,
                Message = null,
            }).ToList(),
        };
    }

    private async Task<IReadOnlyCollection<string>> ListAllNodeIdsAsync(long KnowledgeGraphId, CancellationToken cancellationToken)
    {
        var nodeIds = new List<string>();
        const int pageSize = 500;
        var pageNo = 1;
        while (true)
        {
            var (items, total) = await _store.ListNodesAsync(KnowledgeGraphId, null, null, pageNo, pageSize, cancellationToken);
            if (items.Count == 0)
            {
                break;
            }

            nodeIds.AddRange(items.Select(x => x.Id));
            if (nodeIds.Count >= total)
            {
                break;
            }

            pageNo++;
        }

        return nodeIds;
    }
}
