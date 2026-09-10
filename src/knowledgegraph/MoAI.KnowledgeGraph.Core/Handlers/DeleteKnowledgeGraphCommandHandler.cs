using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphCommand"/>
/// </summary>
public class DeleteKnowledgeGraphCommandHandler : IRequestHandler<DeleteKnowledgeGraphCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);

        // 先清图数据，再软删目录；图库清理失败则整体失败，避免留下孤儿数据
        await _store.PurgeGraphAsync(graph.Id, cancellationToken);

        _databaseContext.KnowledgeGraphs.Remove(graph);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
