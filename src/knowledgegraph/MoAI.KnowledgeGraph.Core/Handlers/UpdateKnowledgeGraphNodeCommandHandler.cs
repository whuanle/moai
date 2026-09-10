using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphNodeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphNodeCommandHandler : IRequestHandler<UpdateKnowledgeGraphNodeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public UpdateKnowledgeGraphNodeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        _ = await _store.GetNodeAsync(request.KgId, request.NodeId, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        var typeExists = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.Id == request.EntityTypeId && x.KgId == request.KgId, cancellationToken);
        if (!typeExists)
        {
            throw new BusinessException("实体类型不存在.") { StatusCode = 400 };
        }

        await _store.UpdateNodeAsync(request.KgId, request.NodeId, request.EntityTypeId, request.Name, request.Description ?? string.Empty, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
