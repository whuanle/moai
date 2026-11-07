using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphsCommand"/>
/// </summary>
public class QueryKnowledgeGraphsCommandHandler : IRequestHandler<QueryKnowledgeGraphsCommand, QueryKnowledgeGraphsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;
    private readonly IKnowledgeGraphIntrospectionCache _introspectionCache;
    private readonly ILogger<QueryKnowledgeGraphsCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    /// <param name="store">图数据存储.</param>
    /// <param name="introspectionCache">接入图内省缓存.</param>
    /// <param name="logger">日志.</param>
    public QueryKnowledgeGraphsCommandHandler(
        DatabaseContext databaseContext,
        IKnowledgeGraphAuthorizer authorizer,
        IKnowledgeGraphSettingsService settingsService,
        IKnowledgeGraphStore store,
        IKnowledgeGraphIntrospectionCache introspectionCache,
        ILogger<QueryKnowledgeGraphsCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
        _store = store;
        _introspectionCache = introspectionCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphsCommandResponse> Handle(QueryKnowledgeGraphsCommand request, CancellationToken cancellationToken)
    {
        var role = await _authorizer.RequireTeamRoleAsync(request.TeamId, adminOnly: false, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);

        var rows = await _databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == request.TeamId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.TeamId,
                x.Name,
                x.Description,
                x.TemplateKey,
                x.Mode,
                x.Database,
                x.AvatarPath,
                x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        var items = new List<KnowledgeGraphItem>(rows.Count);
        foreach (var x in rows)
        {
            // 能力未开启时图库不可用，直接置空避免每个图都白等一次连接超时
            var (entityCount, relationCount) = settings.Enabled
                ? await QueryCountsAsync(x.Id, x.Mode, x.Database, cancellationToken)
                : (null, null);
            items.Add(new KnowledgeGraphItem
            {
                KgId = x.Id,
                TeamId = x.TeamId,
                Name = x.Name,
                Description = x.Description,
                TemplateKey = x.TemplateKey,
                Mode = x.Mode,
                Database = x.Database,
                ReadOnly = x.Mode == KnowledgeGraphModes.Connected,
                AvatarPath = x.AvatarPath,
                EntityCount = entityCount,
                RelationCount = relationCount,
                CreateTime = x.CreateTime,
            });
        }

        return new QueryKnowledgeGraphsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)role,
            Enabled = settings.Enabled,
            Items = items,
        };
    }

    /// <summary>
    /// 统计图谱实体/关系数量：托管图直查图库；接入图经内省缓存（5 分钟）汇总标签计数。
    /// 失败降级为 null（图库不可用不阻断列表）.
    /// </summary>
    private async Task<(int? EntityCount, int? RelationCount)> QueryCountsAsync(long kgId, string mode, string? database, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(database))
                {
                    return (null, null);
                }

                var (introspection, _, _) = await _introspectionCache.GetAsync(kgId, database, refresh: false, cancellationToken);
                return ((int)introspection.Labels.Sum(x => x.Count), (int)introspection.RelationshipTypes.Sum(x => x.Count));
            }

            var nodes = await _store.CountNodesAsync(kgId, cancellationToken);
            var edges = await _store.CountEdgesAsync(kgId, cancellationToken);
            return ((int)nodes, (int)edges);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "统计知识图谱数量失败 kgId={KgId}", kgId);
            return (null, null);
        }
    }
}
