using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphEdgeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphEdgeCommandHandler : IRequestHandler<UpdateKnowledgeGraphEdgeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public UpdateKnowledgeGraphEdgeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var relationType = await _databaseContext.KnowledgeGraphRelationTypes
            .FirstOrDefaultAsync(x => x.Id == request.RelationTypeId && x.KnowledgeGraphId == request.KnowledgeGraphId, cancellationToken)
            ?? throw new BusinessException("关系类型不存在.") { StatusCode = 400 };

        var edge = await _store.GetEdgeAsync(request.KnowledgeGraphId, request.EdgeId, cancellationToken)
            ?? throw new BusinessException("边不存在.") { StatusCode = 404 };

        if (relationType.SourceTypeId != null || relationType.TargetTypeId != null)
        {
            var source = await _store.GetNodeAsync(request.KnowledgeGraphId, edge.SourceNodeId, cancellationToken);
            var target = await _store.GetNodeAsync(request.KnowledgeGraphId, edge.TargetNodeId, cancellationToken);
            if (source == null || target == null)
            {
                throw new BusinessException("边端点节点不存在.") { StatusCode = 400 };
            }

            if (relationType.SourceTypeId != null && relationType.SourceTypeId != source.EntityTypeId)
            {
                throw new BusinessException("起点节点类型不符合关系约束.") { StatusCode = 400 };
            }

            if (relationType.TargetTypeId != null && relationType.TargetTypeId != target.EntityTypeId)
            {
                throw new BusinessException("终点节点类型不符合关系约束.") { StatusCode = 400 };
            }
        }

        var updated = await _store.UpdateEdgeAsync(request.KnowledgeGraphId, request.EdgeId, request.RelationTypeId, cancellationToken);
        if (!updated)
        {
            throw new BusinessException("边不存在.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
