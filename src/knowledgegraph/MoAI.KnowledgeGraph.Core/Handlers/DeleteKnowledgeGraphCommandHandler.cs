using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphCommand"/>
/// </summary>
public class DeleteKnowledgeGraphCommandHandler : IRequestHandler<DeleteKnowledgeGraphCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphIntrospectionCache _introspectionCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    /// <param name="introspectionCache">接入图内省缓存.</param>
    public DeleteKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store, IKnowledgeGraphSettingsService settingsService, IKnowledgeGraphIntrospectionCache introspectionCache)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
        _settingsService = settingsService;
        _introspectionCache = introspectionCache;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        // 能力开启时先清空图库节点与边；图库清理失败则整体失败，避免只删目录留下孤儿数据。
        // 能力未开启时无法连接 Neo4j（此时也不存在可访问的图数据），跳过清理，仅软删数据库元数据。
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (settings.Enabled && string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            await _store.PurgeGraphAsync(graph.Id, cancellationToken);
        }

        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == graph.Id)
            .ToListAsync(cancellationToken);
        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == graph.Id)
            .ToListAsync(cancellationToken);

        _databaseContext.KnowledgeGraphRelationTypes.RemoveRange(relationTypes);
        _databaseContext.KnowledgeGraphEntityTypes.RemoveRange(entityTypes);
        _databaseContext.KnowledgeGraphs.Remove(graph);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(graph.Database))
        {
            await _introspectionCache.RemoveAsync(graph.Id, graph.Database, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
