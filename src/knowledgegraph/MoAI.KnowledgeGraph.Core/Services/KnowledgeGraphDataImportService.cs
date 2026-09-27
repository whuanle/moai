using Maomi;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱结构化导入管线（外部 /import 与内部 /import-json 共用）：
/// 类型按名称解析（可 autoCreateTypes 自动创建）→ 节点按 key（缺失时回退 类型+名称）upsert 匹配 → 批量写入 →
/// 边端点按 nodeId/key/名称引用解析 + 关系约束校验 + upsert 幂等去重 → 批量建边 → 一次节点向量增量.
/// 失败语义：结构性问题（超限/字段非法/模式非法）整单 400；行级问题（类型缺失/引用未命中/约束不符）逐条报告不阻断.
/// validateOnly=true 时全流程只读预演：不建类型、不写图库、不发向量增量，响应为预测值（created 行 id 为 null）.
/// detectDuplicates=true 时对新建节点做疑似重复检测（向量相似度，图谱需已配置向量化模型），失败不影响导入.
/// </summary>
[InjectOnScoped]
public class KnowledgeGraphDataImportService
{
    /// <summary>
    /// 疑似重复检测的单次导入新建节点检查上限（超出部分跳过，控制 embedding 与检索成本）.
    /// </summary>
    public const int MaxDuplicateCheckRows = 100;

    /// <summary>
    /// 每个新建节点最多报告的疑似重复匹配数.
    /// </summary>
    public const int TopMatchesPerRow = 3;

    /// <summary>
    /// 疑似重复判定阈值（Cosine 相似度，0-1）.
    /// </summary>
    public const double DuplicateScoreThreshold = 0.85;

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IKgEmbeddingVectorStore _vectorStore;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<KnowledgeGraphDataImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphDataImportService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    /// <param name="store">图存储.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="vectorStore">知识图谱向量存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public KnowledgeGraphDataImportService(DatabaseContext databaseContext, IKnowledgeGraphSettingsService settingsService, IKnowledgeGraphStore store, IEmbeddingGeneratorProvider embeddingGeneratorProvider, IKgEmbeddingVectorStore vectorStore, IMessagePublisher messagePublisher, ILogger<KnowledgeGraphDataImportService> logger)
    {
        _databaseContext = databaseContext;
        _settingsService = settingsService;
        _store = store;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _vectorStore = vectorStore;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <summary>
    /// 执行导入：入口统一校验 payload（JSON 反序列化入口不经过 MVC 校验层，故在此把关），资源授权由调用方 Handler 完成.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="payload">导入载荷.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="ExternalImportResponse"/>.</returns>
    public async Task<ExternalImportResponse> ImportAsync(long kgId, KnowledgeGraphImportPayload payload, CancellationToken cancellationToken)
    {
        KnowledgeGraphImportPayloadValidator.ValidateOrThrow(payload);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var upsertMode = string.Equals(payload.Mode, ImportExternalGraphDataCommand.ModeUpsert, StringComparison.Ordinal);

        // ===== 1. 载入现有类型 =====
        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == kgId && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);
        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == kgId && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);
        var entityTypeById = entityTypes.ToDictionary(x => x.Id);
        var entityTypeByName = entityTypes.GroupBy(x => x.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var relationTypeById = relationTypes.ToDictionary(x => x.Id);
        var relationTypeByName = relationTypes.GroupBy(x => x.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // ===== 2. 节点行：实体类型解析（id 优先，名称可自动创建）=====
        var nodeRows = new List<NodeRow>(payload.Nodes.Count);
        var pendingEntityTypeNames = new List<string>();
        foreach (var pair in payload.Nodes.Select((item, index) => (item, index)))
        {
            var row = new NodeRow { Index = pair.index, Item = pair.item };
            if (pair.item.EntityTypeId is > 0)
            {
                row.EntityTypeId = pair.item.EntityTypeId.Value;
                if (!entityTypeById.ContainsKey(row.EntityTypeId))
                {
                    row.Fail("实体类型不存在.");
                }
            }
            else
            {
                var typeName = pair.item.EntityTypeName!.Trim();
                if (entityTypeByName.TryGetValue(typeName, out var found))
                {
                    row.EntityTypeId = found.Id;
                }
                else if (payload.AutoCreateTypes)
                {
                    if (!pendingEntityTypeNames.Contains(typeName))
                    {
                        pendingEntityTypeNames.Add(typeName);
                    }
                }
                else
                {
                    row.Fail($"实体类型「{typeName}」不存在且未开启 autoCreateTypes.");
                }
            }

            nodeRows.Add(row);
        }

        // ===== 3. 自动创建缺失实体类型（属性定义=该类型各行属性键并集，均为 string；仅帮助画布渲染，不阻断额外键入库）=====
        // validateOnly 模式不落库：以负数临时 id 供本请求行解析，类型名照常计入 created 清单（预测语义）
        var createdEntityTypeNames = new List<string>();
        if (pendingEntityTypeNames.Count > 0)
        {
            if (payload.ValidateOnly)
            {
                var syntheticId = -1L;
                foreach (var name in pendingEntityTypeNames)
                {
                    var entity = new KnowledgeGraphEntityTypeEntity
                    {
                        Id = syntheticId--,
                        KnowledgeGraphId = kgId,
                        Name = name,
                        Color = string.Empty,
                        Description = string.Empty,
                        Properties = KnowledgeGraphPropertyJson.WriteDefinitions(new List<KnowledgeGraphEntityTypeProperty>()),
                        Sort = 0,
                    };
                    entityTypeByName[entity.Name] = entity;
                    entityTypeById[entity.Id] = entity;
                    createdEntityTypeNames.Add(name);
                }
            }
            else
            {
                var maxSort = await _databaseContext.KnowledgeGraphEntityTypes
                    .Where(x => x.KnowledgeGraphId == kgId)
                    .Select(x => (int?)x.Sort)
                    .MaxAsync(cancellationToken) ?? -1;
                var newTypes = new List<KnowledgeGraphEntityTypeEntity>();
                foreach (var name in pendingEntityTypeNames)
                {
                    var propertyDefs = nodeRows
                        .Where(x => !x.Failed && x.EntityTypeId == 0 && string.Equals(x.Item.EntityTypeName!.Trim(), name, StringComparison.Ordinal))
                        .SelectMany(x => x.Item.Properties ?? new Dictionary<string, string>(StringComparer.Ordinal))
                        .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                        .Select(x => x.Key)
                        .Distinct(StringComparer.Ordinal)
                        .Take(50)
                        .Select(k => new KnowledgeGraphEntityTypeProperty { Name = k, Type = KnowledgeGraphEntityTypeProperty.TypeString })
                        .ToList();
                    var entity = new KnowledgeGraphEntityTypeEntity
                    {
                        KnowledgeGraphId = kgId,
                        Name = name,
                        Color = string.Empty,
                        Description = string.Empty,
                        Properties = KnowledgeGraphPropertyJson.WriteDefinitions(propertyDefs),
                        Sort = ++maxSort,
                    };
                    _databaseContext.KnowledgeGraphEntityTypes.Add(entity);
                    newTypes.Add(entity);
                    createdEntityTypeNames.Add(name);
                }

                await _databaseContext.SaveChangesAsync(cancellationToken);
                foreach (var entity in newTypes)
                {
                    entityTypeByName[entity.Name] = entity;
                    entityTypeById[entity.Id] = entity;
                }
            }

            foreach (var row in nodeRows)
            {
                if (!row.Failed && row.EntityTypeId == 0)
                {
                    row.EntityTypeId = entityTypeByName[row.Item.EntityTypeName!.Trim()].Id;
                }
            }
        }

        // ===== 4. 节点行内去重：key 重复 /（类型+名称）重复 =====
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenTypeNames = new HashSet<(long EntityTypeId, string Name)>();
        foreach (var row in nodeRows)
        {
            if (row.Failed)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Item.Key))
            {
                if (!seenKeys.Add(row.Item.Key))
                {
                    row.Fail("key 在本请求中重复.");
                }

                continue;
            }

            if (!seenTypeNames.Add((row.EntityTypeId, row.Item.Name.Trim())))
            {
                row.Fail("本请求中已存在（实体类型+名称）相同的节点行.");
            }
        }

        // ===== 5. upsert 匹配：key 优先，未命中回退（类型+名称）并收养 key；全部行都参与（类型+名称）预读 =====
        var keysToMatch = new List<string>();
        var pairsToMatch = new List<(long EntityTypeId, string Name)>();
        if (upsertMode)
        {
            foreach (var row in nodeRows)
            {
                if (row.Failed)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(row.Item.Key))
                {
                    keysToMatch.Add(row.Item.Key);
                }

                pairsToMatch.Add((row.EntityTypeId, row.Item.Name.Trim()));
            }
        }

        var existingByKey = upsertMode && keysToMatch.Count > 0
            ? await _store.GetNodesByKeysAsync(kgId, keysToMatch, cancellationToken)
            : new Dictionary<string, KnowledgeGraphNodeRecord>(StringComparer.Ordinal);
        var existingByPair = upsertMode && pairsToMatch.Count > 0
            ? await _store.GetNodesByTypeAndNamesAsync(kgId, pairsToMatch, cancellationToken)
            : new Dictionary<(long, string), KnowledgeGraphNodeRecord>();

        var createInputs = new List<KnowledgeGraphNodeInput>();
        var createRows = new List<NodeRow>();
        var updateInputs = new List<KnowledgeGraphNodeUpdateInput>();
        var updateRows = new List<NodeRow>();
        foreach (var row in nodeRows)
        {
            if (row.Failed)
            {
                continue;
            }

            var name = row.Item.Name.Trim();
            var key = string.IsNullOrWhiteSpace(row.Item.Key) ? null : row.Item.Key;
            KnowledgeGraphNodeRecord? matched = null;
            if (upsertMode)
            {
                if (key != null && !existingByKey.TryGetValue(key, out matched))
                {
                    existingByPair.TryGetValue((row.EntityTypeId, name), out matched);
                }
                else if (key == null)
                {
                    existingByPair.TryGetValue((row.EntityTypeId, name), out matched);
                }
            }

            row.MatchedId = matched?.Id;
            var propsJson = KnowledgeGraphPropertyJson.WriteValues(row.Item.Properties ?? new Dictionary<string, string>(StringComparer.Ordinal));
            if (matched == null)
            {
                createInputs.Add(new KnowledgeGraphNodeInput(row.EntityTypeId, name, row.Item.Description ?? string.Empty, key, propsJson));
                createRows.Add(row);
            }
            else
            {
                updateInputs.Add(new KnowledgeGraphNodeUpdateInput(matched.Id, row.EntityTypeId, name, row.Item.Description ?? string.Empty, propsJson, key));
                updateRows.Add(row);
            }
        }

        // ===== 6. 节点写入（新建带 key/属性；更新整体覆盖属性，key=null 保留现有 key）；validateOnly 跳过写图 =====
        if (payload.ValidateOnly)
        {
            foreach (var row in createRows)
            {
                row.FinalId = $"pending-{row.Index}";
            }

            foreach (var row in updateRows)
            {
                row.FinalId = row.MatchedId;
            }
        }
        else
        {
            if (createInputs.Count > 0)
            {
                var createdRecords = await _store.CreateNodesBatchAsync(kgId, createInputs, cancellationToken);
                for (var i = 0; i < createRows.Count; i++)
                {
                    createRows[i].FinalId = createdRecords[i].Id;
                }
            }

            if (updateInputs.Count > 0)
            {
                await _store.UpdateNodesBatchAsync(kgId, updateInputs, cancellationToken);
                foreach (var row in updateRows)
                {
                    row.FinalId = row.MatchedId;
                }
            }
        }

        // ===== 7. 本请求节点解析映射（边端点引用优先命中）=====
        var requestKeyMap = new Dictionary<string, (string Id, long EntityTypeId)>(StringComparer.Ordinal);
        var requestPairMap = new Dictionary<(long EntityTypeId, string Name), (string Id, long EntityTypeId)>();
        var requestNameMap = new Dictionary<string, List<(string Id, long EntityTypeId)>>(StringComparer.Ordinal);
        foreach (var row in nodeRows)
        {
            if (row.Failed || row.FinalId == null)
            {
                continue;
            }

            var name = row.Item.Name.Trim();
            if (!string.IsNullOrWhiteSpace(row.Item.Key))
            {
                requestKeyMap[row.Item.Key] = (row.FinalId, row.EntityTypeId);
            }

            requestPairMap[(row.EntityTypeId, name)] = (row.FinalId, row.EntityTypeId);
            if (!requestNameMap.TryGetValue(name, out var entries))
            {
                entries = new List<(string, long)>();
                requestNameMap[name] = entries;
            }

            entries.Add((row.FinalId, row.EntityTypeId));
        }

        // ===== 8. 边行：关系类型解析（自动创建时约束为任意）=====
        var edgeRows = new List<EdgeRow>(payload.Edges.Count);
        var pendingRelationTypeNames = new List<string>();
        foreach (var pair in payload.Edges.Select((item, index) => (item, index)))
        {
            var row = new EdgeRow { Index = pair.index, Item = pair.item };
            if (pair.item.RelationTypeId is > 0)
            {
                row.RelationTypeId = pair.item.RelationTypeId.Value;
                if (!relationTypeById.ContainsKey(row.RelationTypeId))
                {
                    row.Fail("关系类型不存在.");
                }
            }
            else
            {
                var typeName = pair.item.RelationTypeName!.Trim();
                if (relationTypeByName.TryGetValue(typeName, out var found))
                {
                    row.RelationTypeId = found.Id;
                }
                else if (payload.AutoCreateTypes)
                {
                    if (!pendingRelationTypeNames.Contains(typeName))
                    {
                        pendingRelationTypeNames.Add(typeName);
                    }
                }
                else
                {
                    row.Fail($"关系类型「{typeName}」不存在且未开启 autoCreateTypes.");
                }
            }

            edgeRows.Add(row);
        }

        var createdRelationTypeNames = new List<string>();
        if (pendingRelationTypeNames.Count > 0)
        {
            if (payload.ValidateOnly)
            {
                var syntheticId = -1_000_000L;
                foreach (var name in pendingRelationTypeNames)
                {
                    var entity = new KnowledgeGraphRelationTypeEntity
                    {
                        Id = syntheticId--,
                        KnowledgeGraphId = kgId,
                        Name = name,
                        Color = string.Empty,
                        Description = string.Empty,
                        SourceTypeId = null,
                        TargetTypeId = null,
                        Sort = 0,
                    };
                    relationTypeByName[entity.Name] = entity;
                    relationTypeById[entity.Id] = entity;
                    createdRelationTypeNames.Add(name);
                }
            }
            else
            {
                var maxSort = await _databaseContext.KnowledgeGraphRelationTypes
                    .Where(x => x.KnowledgeGraphId == kgId)
                    .Select(x => (int?)x.Sort)
                    .MaxAsync(cancellationToken) ?? -1;
                var newTypes = new List<KnowledgeGraphRelationTypeEntity>();
                foreach (var name in pendingRelationTypeNames)
                {
                    var entity = new KnowledgeGraphRelationTypeEntity
                    {
                        KnowledgeGraphId = kgId,
                        Name = name,
                        Color = string.Empty,
                        Description = string.Empty,
                        SourceTypeId = null,
                        TargetTypeId = null,
                        Sort = ++maxSort,
                    };
                    _databaseContext.KnowledgeGraphRelationTypes.Add(entity);
                    newTypes.Add(entity);
                    createdRelationTypeNames.Add(name);
                }

                await _databaseContext.SaveChangesAsync(cancellationToken);
                foreach (var entity in newTypes)
                {
                    relationTypeByName[entity.Name] = entity;
                    relationTypeById[entity.Id] = entity;
                }
            }

            foreach (var row in edgeRows)
            {
                if (!row.Failed && row.RelationTypeId == 0)
                {
                    row.RelationTypeId = relationTypeByName[row.Item.RelationTypeName!.Trim()].Id;
                }
            }
        }

        // ===== 9. 端点批量预取（nodeId / key /（类型,名称）/ 仅名称 四类引用一次读回）=====
        var refNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var refKeys = new HashSet<string>(StringComparer.Ordinal);
        var refPairs = new List<(long EntityTypeId, string Name)>();
        var refNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in edgeRows)
        {
            if (row.Failed)
            {
                continue;
            }

            foreach (var nodeRef in new[] { row.Item.Source, row.Item.Target })
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
                    var typeName = nodeRef.EntityTypeName.Trim();
                    if (entityTypeByName.TryGetValue(typeName, out var typeEntity))
                    {
                        refPairs.Add((typeEntity.Id, nodeRef.Name!.Trim()));
                    }
                    else
                    {
                        row.Fail($"端点实体类型「{typeName}」不存在.");
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
        var refKeyNodes = refKeys.Count > 0
            ? await _store.GetNodesByKeysAsync(kgId, refKeys.ToList(), cancellationToken)
            : new Dictionary<string, KnowledgeGraphNodeRecord>(StringComparer.Ordinal);
        var refPairNodes = refPairs.Count > 0
            ? await _store.GetNodesByTypeAndNamesAsync(kgId, refPairs, cancellationToken)
            : new Dictionary<(long, string), KnowledgeGraphNodeRecord>();
        var refNameGroups = refNames.Count > 0
            ? (await _store.GetNodesByNamesAsync(kgId, refNames.ToList(), cancellationToken))
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal)
            : new Dictionary<string, List<KnowledgeGraphNodeRecord>>(StringComparer.Ordinal);

        (string? Id, long EntityTypeId) ResolveEndpoint(EdgeRow row, bool isSource)
        {
            var nodeRef = isSource ? row.Item.Source : row.Item.Target;
            var label = isSource ? "起点" : "终点";
            if (!string.IsNullOrWhiteSpace(nodeRef.NodeId))
            {
                if (nodeTypesByIds.TryGetValue(nodeRef.NodeId, out var entityTypeId))
                {
                    return (nodeRef.NodeId, entityTypeId);
                }

                row.Fail($"{label}节点不存在.");
                return (null, 0);
            }

            if (!string.IsNullOrWhiteSpace(nodeRef.Key))
            {
                if (requestKeyMap.TryGetValue(nodeRef.Key, out var hit))
                {
                    return hit;
                }

                if (refKeyNodes.TryGetValue(nodeRef.Key, out var record))
                {
                    return (record.Id, record.EntityTypeId);
                }

                row.Fail($"{label}节点不存在（key 未命中）.");
                return (null, 0);
            }

            var name = nodeRef.Name!.Trim();
            if (!string.IsNullOrWhiteSpace(nodeRef.EntityTypeName))
            {
                var typeName = nodeRef.EntityTypeName.Trim();
                if (!entityTypeByName.TryGetValue(typeName, out var typeEntity))
                {
                    row.Fail($"{label}实体类型「{typeName}」不存在.");
                    return (null, 0);
                }

                if (requestPairMap.TryGetValue((typeEntity.Id, name), out var pairHit))
                {
                    return pairHit;
                }

                if (refPairNodes.TryGetValue((typeEntity.Id, name), out var pairRecord))
                {
                    return (pairRecord.Id, pairRecord.EntityTypeId);
                }

                row.Fail($"{label}节点不存在（实体类型「{typeName}」下无名称「{name}」）.");
                return (null, 0);
            }

            if (requestNameMap.TryGetValue(name, out var entries))
            {
                if (entries.Count > 1)
                {
                    row.Fail($"{label}名称「{name}」命中多个节点，请补充 entityTypeName 或改用 key 引用.");
                    return (null, 0);
                }

                return entries[0];
            }

            if (refNameGroups.TryGetValue(name, out var records))
            {
                if (records.Count > 1)
                {
                    row.Fail($"{label}名称「{name}」命中多个节点，请补充 entityTypeName 或改用 key 引用.");
                    return (null, 0);
                }

                return (records[0].Id, records[0].EntityTypeId);
            }

            row.Fail($"{label}节点不存在（名称「{name}」未命中）.");
            return (null, 0);
        }

        foreach (var row in edgeRows)
        {
            if (row.Failed)
            {
                continue;
            }

            var source = ResolveEndpoint(row, isSource: true);
            if (row.Failed)
            {
                continue;
            }

            var target = ResolveEndpoint(row, isSource: false);
            if (row.Failed)
            {
                continue;
            }

            row.SourceId = source.Id;
            row.SourceEntityTypeId = source.EntityTypeId;
            row.TargetId = target.Id;
            row.TargetEntityTypeId = target.EntityTypeId;
        }

        // ===== 10. 关系约束校验 + upsert 幂等去重 + 建边 =====
        var pendingTriples = edgeRows
            .Where(x => !x.Failed)
            .Select(x => (x.SourceId!, x.RelationTypeId, x.TargetId!))
            .ToList();
        var existingEdgeKeys = upsertMode && pendingTriples.Count > 0
            ? await _store.GetExistingEdgeKeysAsync(kgId, pendingTriples, cancellationToken)
            : new HashSet<string>(StringComparer.Ordinal);

        var seenTriples = new HashSet<string>(StringComparer.Ordinal);
        var edgeInputs = new List<KnowledgeGraphEdgeInput>();
        var edgeCreateRows = new List<EdgeRow>();
        foreach (var row in edgeRows)
        {
            if (row.Failed)
            {
                continue;
            }

            var relationType = relationTypeById[row.RelationTypeId];
            if ((relationType.SourceTypeId != null && relationType.SourceTypeId != row.SourceEntityTypeId)
                || (relationType.TargetTypeId != null && relationType.TargetTypeId != row.TargetEntityTypeId))
            {
                row.Fail("起点或终点节点类型不符合关系约束.");
                continue;
            }

            if (upsertMode)
            {
                var tripleKey = $"{row.SourceId}|{row.RelationTypeId}|{row.TargetId}";
                if (seenTriples.Contains(tripleKey) || existingEdgeKeys.Contains(tripleKey))
                {
                    row.Skip = true;
                    continue;
                }

                seenTriples.Add(tripleKey);
            }

            edgeInputs.Add(new KnowledgeGraphEdgeInput(row.RelationTypeId, row.SourceId!, row.TargetId!));
            edgeCreateRows.Add(row);
        }

        if (!payload.ValidateOnly && edgeInputs.Count > 0)
        {
            var edgeRecords = await _store.CreateEdgesBatchAsync(kgId, edgeInputs, cancellationToken);
            for (var i = 0; i < edgeCreateRows.Count; i++)
            {
                edgeCreateRows[i].CreatedId = edgeRecords[i].Id;
            }
        }

        // ===== 11. 逐条结果 + 一次节点向量增量（validateOnly 全部跳过写入侧）=====
        var results = nodeRows.OrderBy(x => x.Index).Select(row => ToNodeResult(row, payload.ValidateOnly))
            .Concat(edgeRows.OrderBy(x => x.Index).Select(row => ToEdgeResult(row, payload.ValidateOnly)))
            .ToList();

        var affectedNodeIds = nodeRows
            .Where(x => !x.Failed && x.FinalId != null)
            .Select(x => x.FinalId!)
            .ToList();
        if (!payload.ValidateOnly && affectedNodeIds.Count > 0)
        {
            await KgEmbeddingDeltaPublisher.PublishNodesUpsertAsync(_messagePublisher, _logger, kgId, affectedNodeIds);
        }

        // ===== 12. 疑似重复检测（对新建行：向量相似度对图谱已有节点 + 批内两两；失败不影响导入）=====
        var duplicateSuspects = payload.DetectDuplicates
            ? await DetectDuplicatesAsync(kgId, nodeRows.Where(x => !x.Failed && x.MatchedId == null).ToList(), cancellationToken)
            : new List<ImportDuplicateSuspect>();

        return new ExternalImportResponse
        {
            NodeCreatedCount = nodeRows.Count(x => !x.Failed && x.MatchedId == null),
            NodeUpdatedCount = nodeRows.Count(x => !x.Failed && x.MatchedId != null),
            NodeFailedCount = nodeRows.Count(x => x.Failed),
            EdgeCreatedCount = edgeRows.Count(x => !x.Failed && !x.Skip),
            EdgeSkippedCount = edgeRows.Count(x => !x.Failed && x.Skip),
            EdgeFailedCount = edgeRows.Count(x => x.Failed),
            CreatedEntityTypeNames = createdEntityTypeNames,
            CreatedRelationTypeNames = createdRelationTypeNames,
            Results = results,
            DuplicateSuspects = duplicateSuspects,
        };
    }

    /// <summary>
    /// 疑似重复检测：对每个新建节点，用「名称\n描述」（与 KgEmbeddingService 向量化契约一致）生成向量后
    /// 检索图谱向量集合（对照已有节点），并对候选行做批内两两余弦；任何失败仅记日志返回空（不阻断导入）.
    /// 新建节点的向量经 MQ 异步入库，检索命中天然以既有节点为主；本请求节点的 id 一并排除以兜竞态.
    /// </summary>
    private async Task<List<ImportDuplicateSuspect>> DetectDuplicatesAsync(long kgId, IReadOnlyList<NodeRow> createRows, CancellationToken cancellationToken)
    {
        var suspects = new List<ImportDuplicateSuspect>();
        if (createRows.Count == 0)
        {
            return suspects;
        }

        try
        {
            var graph = await _databaseContext.KnowledgeGraphs
                .AsNoTracking()
                .Where(x => x.Id == kgId)
                .Select(x => new { x.TeamId, x.EmbeddingModelId, x.EmbeddingDimensions })
                .FirstOrDefaultAsync(cancellationToken);
            // Guid? 判空用 == null，禁止 == Guid.Empty 哨兵（对齐 GraphSearchService/KgEmbeddingService）
            if (graph == null || graph.EmbeddingModelId == null || graph.EmbeddingDimensions <= 0)
            {
                _logger.LogInformation("导入疑似重复检测跳过：图谱未配置向量化模型. KgId={KgId}", kgId);
                return suspects;
            }

            var modelId = graph.EmbeddingModelId.Value;
            var query = from m in _databaseContext.AiModels
                        join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                        where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                        select new { m, c };
            var candidates = await query.ToListAsync(cancellationToken);
            var model = candidates.FirstOrDefault(x => x.m.IsPublic);
            if (model == null)
            {
                var authorized = await _databaseContext.AiModelAuthorizations
                    .AnyAsync(x => x.AiModelId == modelId && x.TeamId == graph.TeamId, cancellationToken);
                if (authorized)
                {
                    model = candidates.FirstOrDefault();
                }
            }

            if (model == null)
            {
                _logger.LogInformation("导入疑似重复检测跳过：向量化模型不可用或未授权. KgId={KgId} ModelId={ModelId}", kgId, modelId);
                return suspects;
            }

            var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(model.m, model.c, cancellationToken);
            var rows = createRows.Take(MaxDuplicateCheckRows).ToList();
            var texts = rows.Select(x => $"{x.Item.Name.Trim()}\n{x.Item.Description ?? string.Empty}").ToList();
            var embeddings = await generator.GenerateAsync(
                texts,
                options: new EmbeddingGenerationOptions { Dimensions = graph.EmbeddingDimensions },
                cancellationToken: cancellationToken);

            // 批内两两余弦（同一批向量直接内存计算）
            var vectors = embeddings.Select(x => x?.Vector).ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                if (vectors[i] is null || vectors[i]!.Value.IsEmpty)
                {
                    continue;
                }

                for (var j = i + 1; j < rows.Count; j++)
                {
                    if (vectors[j] is null || vectors[j]!.Value.IsEmpty)
                    {
                        continue;
                    }

                    var score = CosineSimilarity(vectors[i]!.Value.Span, vectors[j]!.Value.Span);
                    if (score >= DuplicateScoreThreshold)
                    {
                        suspects.Add(new ImportDuplicateSuspect
                        {
                            Index = rows[i].Index,
                            Name = rows[i].Item.Name.Trim(),
                            Kind = "inbatch",
                            Score = score,
                            MatchIndex = rows[j].Index,
                            MatchName = rows[j].Item.Name.Trim(),
                        });
                    }
                }
            }

            // 对照图谱已有节点（向量集合检索；本请求节点 id 排除以兜 MQ 异步入库的竞态）
            var requestNodeIds = createRows
                .Where(x => x.FinalId != null && !x.FinalId.StartsWith("pending-", StringComparison.Ordinal))
                .Select(x => x.FinalId!)
                .ToHashSet(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                if (vectors[i] is null || vectors[i]!.Value.IsEmpty)
                {
                    continue;
                }

                var hits = await _vectorStore.SearchAsync(kgId, vectors[i]!.Value, TopMatchesPerRow, cancellationToken);
                foreach (var hit in hits)
                {
                    if (requestNodeIds.Contains(hit.Record.NodeId))
                    {
                        continue;
                    }

                    if ((hit.Score ?? double.MinValue) < DuplicateScoreThreshold)
                    {
                        continue;
                    }

                    suspects.Add(new ImportDuplicateSuspect
                    {
                        Index = rows[i].Index,
                        Name = rows[i].Item.Name.Trim(),
                        Kind = "existing",
                        Score = hit.Score ?? 0,
                        MatchNodeId = hit.Record.NodeId,
                        MatchName = hit.Record.Name,
                    });
                }
            }

            return suspects.OrderByDescending(x => x.Score).Take(MaxDuplicateCheckRows * TopMatchesPerRow).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导入疑似重复检测失败（不影响导入）. KgId={KgId}", kgId);
            return new List<ImportDuplicateSuspect>();
        }
    }

    private static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length == 0 || left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0, normLeft = 0, normRight = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            normLeft += left[i] * left[i];
            normRight += right[i] * right[i];
        }

        if (normLeft == 0 || normRight == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight));
    }

    private static ExternalImportItemResult ToNodeResult(NodeRow row, bool validateOnly) => new()
    {
        Kind = "node",
        Index = row.Index,
        Ok = !row.Failed,
        Id = row.Failed || validateOnly ? null : row.FinalId,
        Action = row.Failed ? null : row.MatchedId == null ? "created" : "updated",
        Message = row.Error,
    };

    private static ExternalImportItemResult ToEdgeResult(EdgeRow row, bool validateOnly) => new()
    {
        Kind = "edge",
        Index = row.Index,
        Ok = !row.Failed,
        Id = row.CreatedId is null || validateOnly ? null : row.CreatedId,
        Action = row.Failed ? null : row.Skip ? "skipped" : "created",
        Message = row.Error,
    };

    private sealed class NodeRow
    {
        public int Index { get; init; }

        public ExternalImportNodeItem Item { get; init; } = default!;

        /// <summary>
        /// 解析后的实体类型 id；0 表示待自动创建回填.
        /// </summary>
        public long EntityTypeId { get; set; }

        public bool Failed { get; private set; }

        public string? Error { get; private set; }

        /// <summary>
        /// upsert 匹配到的现有节点 id；null 表示新建.
        /// </summary>
        public string? MatchedId { get; set; }

        /// <summary>
        /// 最终节点 id（新建回填或匹配 id）.
        /// </summary>
        public string? FinalId { get; set; }

        public void Fail(string message)
        {
            Failed = true;
            Error = message;
        }
    }

    private sealed class EdgeRow
    {
        public int Index { get; init; }

        public ExternalImportEdgeItem Item { get; init; } = default!;

        /// <summary>
        /// 解析后的关系类型 id；0 表示待自动创建回填.
        /// </summary>
        public long RelationTypeId { get; set; }

        public bool Failed { get; private set; }

        public string? Error { get; private set; }

        /// <summary>
        /// upsert 幂等跳过（边已存在或本请求重复）.
        /// </summary>
        public bool Skip { get; set; }

        public string? SourceId { get; set; }

        public long SourceEntityTypeId { get; set; }

        public string? TargetId { get; set; }

        public long TargetEntityTypeId { get; set; }

        public string? CreatedId { get; set; }

        public void Fail(string message)
        {
            Failed = true;
            Error = message;
        }
    }
}
