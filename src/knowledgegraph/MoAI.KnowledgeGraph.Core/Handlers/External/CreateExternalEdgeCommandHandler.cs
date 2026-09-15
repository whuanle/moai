using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalEdgeCommand"/>
/// </summary>
public class CreateExternalEdgeCommandHandler : IRequestHandler<CreateExternalEdgeCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public CreateExternalEdgeCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(CreateExternalEdgeCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);

        var relationType = await _databaseContext.KnowledgeGraphRelationTypes
            .FirstOrDefaultAsync(x => x.Id == request.RelationTypeId && x.KnowledgeGraphId == request.KnowledgeGraphId, cancellationToken)
            ?? throw new BusinessException("关系类型不存在.") { StatusCode = 400 };

        var source = await _store.GetNodeAsync(request.KnowledgeGraphId, request.SourceNodeId, cancellationToken)
            ?? throw new BusinessException("起点节点不存在.") { StatusCode = 400 };
        var target = await _store.GetNodeAsync(request.KnowledgeGraphId, request.TargetNodeId, cancellationToken)
            ?? throw new BusinessException("终点节点不存在.") { StatusCode = 400 };

        if (relationType.SourceTypeId != null && relationType.SourceTypeId != source.EntityTypeId)
        {
            throw new BusinessException("起点节点类型不符合关系约束.") { StatusCode = 400 };
        }

        if (relationType.TargetTypeId != null && relationType.TargetTypeId != target.EntityTypeId)
        {
            throw new BusinessException("终点节点类型不符合关系约束.") { StatusCode = 400 };
        }

        var edge = await _store.CreateEdgeAsync(request.KnowledgeGraphId, request.RelationTypeId, request.SourceNodeId, request.TargetNodeId, cancellationToken);
        return new SimpleString { Value = edge.Id };
    }
}
