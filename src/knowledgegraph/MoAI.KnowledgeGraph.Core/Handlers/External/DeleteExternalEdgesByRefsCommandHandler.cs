using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteExternalEdgesByRefsCommand"/>
/// 按端点引用批量删除边：关系类型按 id/名称解析（不存在按行失败），端点按 nodeId/key/名称解析（同导入口径），
/// 解析成功的三元组执行删除（同三元组平行边一并删除；端点存在但无边为幂等成功计 0）.
/// </summary>
public class DeleteExternalEdgesByRefsCommandHandler : IRequestHandler<DeleteExternalEdgesByRefsCommand, DeleteExternalEdgesByRefsResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteExternalEdgesByRefsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public DeleteExternalEdgesByRefsCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<DeleteExternalEdgesByRefsResponse> Handle(DeleteExternalEdgesByRefsCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);
        var kgId = request.KnowledgeGraphId;

        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == kgId && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);
        var relationTypeById = relationTypes.ToDictionary(x => x.Id);
        var relationTypeByName = relationTypes.GroupBy(x => x.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // 端点批量预取（nodeId / key /（类型,名称）/ 仅名称 四类引用一次读回）
        var refNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var refKeys = new HashSet<string>(StringComparer.Ordinal);
        var refPairs = new List<(long EntityTypeId, string Name)>();
        var refNames = new HashSet<string>(StringComparer.Ordinal);
        var entityTypeByName = (await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == kgId && x.IsDeleted == 0)
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var item in request.Items)
        {
            foreach (var nodeRef in new[] { item.Source, item.Target })
            {
                if (!string.IsNullOrWhiteSpace(nodeRef.NodeId))
                {
                    refNodeIds.Add(nodeRef.NodeId);
                }
                else if (!string.IsNullOrWhiteSpace(nodeRef.Key))
                {
                    refKeys.Add(nodeRef.Key);
                }
                else if (!string.IsNullOrWhiteSpace(nodeRef.EntityTypeName))
                {
                    if (entityTypeByName.TryGetValue(nodeRef.EntityTypeName.Trim(), out var typeEntity))
                    {
                        refPairs.Add((typeEntity.Id, nodeRef.Name!.Trim()));
                    }
                }
                else
                {
                    refNames.Add(nodeRef.Name!.Trim());
                }
            }
        }

        var nodeTypesByIds = refNodeIds.Count > 0
            ? await _store.GetNodeTypesByIdsAsync(kgId, refNodeIds.ToList(), cancellationToken)
            : new Dictionary<string, long>(StringComparer.Ordinal);
        var keyNodes = refKeys.Count > 0
            ? await _store.GetNodesByKeysAsync(kgId, refKeys.ToList(), cancellationToken)
            : new Dictionary<string, KnowledgeGraphNodeRecord>(StringComparer.Ordinal);
        var pairNodes = refPairs.Count > 0
            ? await _store.GetNodesByTypeAndNamesAsync(kgId, refPairs, cancellationToken)
            : new Dictionary<(long, string), KnowledgeGraphNodeRecord>();
        var nameGroups = refNames.Count > 0
            ? (await _store.GetNodesByNamesAsync(kgId, refNames.ToList(), cancellationToken))
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal)
            : new Dictionary<string, List<KnowledgeGraphNodeRecord>>(StringComparer.Ordinal);

        (string? Id, bool Failed, string? Error) Resolve(bool isSource, ExternalImportNodeRef nodeRef, int index)
        {
            var label = isSource ? "起点" : "终点";
            if (!string.IsNullOrWhiteSpace(nodeRef.NodeId))
            {
                if (nodeTypesByIds.TryGetValue(nodeRef.NodeId, out _))
                {
                    return (nodeRef.NodeId, false, null);
                }

                return (null, true, $"第 {index} 行{label}节点不存在.");
            }

            if (!string.IsNullOrWhiteSpace(nodeRef.Key))
            {
                return keyNodes.TryGetValue(nodeRef.Key, out var record)
                    ? (record.Id, false, null)
                    : (null, true, $"第 {index} 行{label}节点不存在（key 未命中）.");
            }

            var name = nodeRef.Name!.Trim();
            if (!string.IsNullOrWhiteSpace(nodeRef.EntityTypeName))
            {
                var typeName = nodeRef.EntityTypeName.Trim();
                if (!entityTypeByName.TryGetValue(typeName, out var typeEntity))
                {
                    return (null, true, $"第 {index} 行{label}实体类型「{typeName}」不存在.");
                }

                return pairNodes.TryGetValue((typeEntity.Id, name), out var record)
                    ? (record.Id, false, null)
                    : (null, true, $"第 {index} 行{label}节点不存在（实体类型「{typeName}」下无名称「{name}」）.");
            }

            if (nameGroups.TryGetValue(name, out var records) && records.Count == 1)
            {
                return (records[0].Id, false, null);
            }

            return (null, true, records != null
                ? $"第 {index} 行{label}名称「{name}」命中多个节点，请补充 entityTypeName 或改用 key 引用."
                : $"第 {index} 行{label}节点不存在（名称「{name}」未命中）.");
        }

        var pendingTriples = new List<(string SourceNodeId, long RelationTypeId, string TargetNodeId)>();
        var rowTriples = new (string? TripleKey, string? Error)[request.Items.Count];
        var seenTriples = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            long relationTypeId;
            if (item.RelationTypeId is > 0)
            {
                if (!relationTypeById.ContainsKey(item.RelationTypeId.Value))
                {
                    rowTriples[index] = (null, $"第 {index} 行关系类型不存在.");
                    continue;
                }

                relationTypeId = item.RelationTypeId.Value;
            }
            else
            {
                var typeName = item.RelationTypeName!.Trim();
                if (!relationTypeByName.TryGetValue(typeName, out var relationType))
                {
                    rowTriples[index] = (null, $"第 {index} 行关系类型「{typeName}」不存在.");
                    continue;
                }

                relationTypeId = relationType.Id;
            }

            var source = Resolve(isSource: true, item.Source, index);
            if (source.Failed)
            {
                rowTriples[index] = (null, source.Error);
                continue;
            }

            var target = Resolve(isSource: false, item.Target, index);
            if (target.Failed)
            {
                rowTriples[index] = (null, target.Error);
                continue;
            }

            var tripleKey = $"{source.Id}|{relationTypeId}|{target.Id}";
            if (!seenTriples.Add(tripleKey))
            {
                // 重复行合并为一次删除（同三元组平行边本就全删）
                rowTriples[index] = (tripleKey, null);
                continue;
            }

            pendingTriples.Add((source.Id!, relationTypeId, target.Id!));
            rowTriples[index] = (tripleKey, null);
        }

        var deletedIds = pendingTriples.Count > 0
            ? await _store.DeleteEdgesByTriplesAsync(kgId, pendingTriples, cancellationToken)
            : [];

        return new DeleteExternalEdgesByRefsResponse
        {
            DeletedCount = deletedIds.Count,
            Results = rowTriples.Select((row, index) => new ExternalEdgeDeleteItemResult
            {
                Index = index,
                Ok = row.Error == null,
                Message = row.Error,
            }).ToList(),
        };
    }
}
