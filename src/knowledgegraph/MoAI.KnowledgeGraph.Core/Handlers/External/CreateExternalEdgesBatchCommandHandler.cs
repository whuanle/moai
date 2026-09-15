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

        // 逐条业务校验，任一失败整批拒绝（不触发图库写入）；校验语义与单条创建一致（含关系类型端点约束）.
        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
            .Select(x => new { x.Id, x.SourceTypeId, x.TargetTypeId })
            .ToListAsync(cancellationToken);
        var relationTypeMap = relationTypes.ToDictionary(x => x.Id);

        // 端点预检：一次查询取回相关节点（存在性 + 实体类型，供关系约束校验），缺失 key 即节点不存在.
        var endpointIds = request.Items
            .SelectMany(x => new[] { x.SourceNodeId, x.TargetNodeId })
            .Distinct()
            .ToList();
        var nodeTypes = await _store.GetNodeTypesByIdsAsync(request.KnowledgeGraphId, endpointIds, cancellationToken);

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (!relationTypeMap.TryGetValue(item.RelationTypeId, out var relationType))
            {
                throw new BusinessException($"第 {index} 条校验失败：关系类型不存在.") { StatusCode = 400 };
            }

            if (!nodeTypes.TryGetValue(item.SourceNodeId, out var sourceEntityTypeId))
            {
                throw new BusinessException($"第 {index} 条校验失败：起点节点不存在.") { StatusCode = 400 };
            }

            if (!nodeTypes.TryGetValue(item.TargetNodeId, out var targetEntityTypeId))
            {
                throw new BusinessException($"第 {index} 条校验失败：终点节点不存在.") { StatusCode = 400 };
            }

            if (relationType.SourceTypeId != null && relationType.SourceTypeId != sourceEntityTypeId)
            {
                throw new BusinessException($"第 {index} 条校验失败：起点节点类型不符合关系约束.") { StatusCode = 400 };
            }

            if (relationType.TargetTypeId != null && relationType.TargetTypeId != targetEntityTypeId)
            {
                throw new BusinessException($"第 {index} 条校验失败：终点节点类型不符合关系约束.") { StatusCode = 400 };
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
}
