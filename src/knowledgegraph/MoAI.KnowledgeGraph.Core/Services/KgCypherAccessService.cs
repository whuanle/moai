using System.Globalization;
using System.Text.Json;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Models;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱 Cypher 只读访问服务实现：供动态插件 kg_cypher_query 消费，托管图强制 $kgId 隔离，接入图按库路由只读会话.
/// </summary>
[InjectOnScoped]
public class KgCypherAccessService : IKgCypherAccessService
{
    private const int MaxCellDepth = 3;
    private const int MaxCellLength = 2000;
    private const int MaxCellChildren = 50;
    private const string CellTruncatedSuffix = "…(已截断)";

    /// <summary>
    /// 托管图用法说明（固定文案，随摘要返回给对话模型）.
    /// </summary>
    private const string ManagedUsageText = "托管图谱：节点标签固定为 KgNode（属性 id/kgId/entityTypeId/name/description/propsJson），边类型固定为 KG_REL（属性 relationTypeId）。所有 MATCH 必须带 {kgId: $kgId} 过滤，$kgId 由系统自动注入、请勿自行赋值；写操作不支持。";

    private const string ManagedMissingKgIdMessage = "托管图谱查询必须包含 {kgId: $kgId} 过滤以隔离图谱数据，例如：MATCH (n:KgNode {kgId: $kgId}) RETURN n.name LIMIT 20；$kgId 参数由系统自动注入，请在查询中使用后重试。注意 $kgId 必须作为过滤参数出现在 MATCH/WHERE 中，写在字符串字面量内无效";

    private const string ManagedKgIdMismatchMessage = "查询结果包含其他图谱的数据，已拒绝返回：请确认查询带有 {kgId: $kgId} 过滤";

    private const string UnavailableMessage = "无法连接图数据库，请检查系统设置中的连接配置。";

    private readonly DatabaseContext _databaseContext;
    private readonly GraphDriverProvider _provider;
    private readonly IKnowledgeGraphIntrospectionCache _introspectionCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="KgCypherAccessService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="provider">图数据库驱动提供者.</param>
    /// <param name="introspectionCache">接入图内省缓存.</param>
    public KgCypherAccessService(DatabaseContext databaseContext, GraphDriverProvider provider, IKnowledgeGraphIntrospectionCache introspectionCache)
    {
        _databaseContext = databaseContext;
        _provider = provider;
        _introspectionCache = introspectionCache;
    }

    /// <inheritdoc/>
    public async Task<KgCypherQueryResult> ExecuteQueryAsync(long knowledgeGraphId, string cypher, IReadOnlyDictionary<string, object?>? parameters, int maxRows, int timeoutSeconds, CancellationToken cancellationToken)
    {
        maxRows = Math.Clamp(maxRows, 1, 1000);
        var graph = await GetGraphAsync(knowledgeGraphId, cancellationToken);
        var isManaged = !string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal);
        if (isManaged && !cypher.Contains("$kgId", StringComparison.Ordinal))
        {
            throw new BusinessException(ManagedMissingKgIdMessage) { StatusCode = 400 };
        }

        if (!isManaged && string.IsNullOrWhiteSpace(graph.Database))
        {
            throw new BusinessException("接入图谱缺少数据库配置.") { StatusCode = 409 };
        }

        // 参数复制为 Ordinal 字典；托管图强制覆盖 kgId，防止用户同名参数绕过图谱隔离.
        var queryParameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters != null)
        {
            foreach (var pair in parameters)
            {
                queryParameters[pair.Key] = pair.Value;
            }
        }

        if (isManaged)
        {
            queryParameters["kgId"] = knowledgeGraphId;
        }

        var clampedTimeout = Math.Clamp(timeoutSeconds, 1, 300);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(clampedTimeout));

        var (driver, dialect) = await _provider.GetRuntimeAsync(timeoutCts.Token);
        await using var session = OpenSession(driver, isManaged ? null : graph.Database, dialect);

        List<IRecord> records;
        try
        {
            records = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, queryParameters);
                var rows = new List<IRecord>(maxRows + 1);

                // 只取 maxRows + 1 行即可判定截断，避免无 LIMIT 查询拉取全量结果.
                while (rows.Count <= maxRows)
                {
                    timeoutCts.Token.ThrowIfCancellationRequested();
                    if (!await cursor.FetchAsync())
                    {
                        break;
                    }

                    rows.Add(cursor.Current);
                }

                return rows;
            }, config => config.WithTimeout(TimeSpan.FromSeconds(clampedTimeout)));
        }

        // 异常映射与 CypherKnowledgeGraphStore 保持同步（勿单方修改词法/路由语义）.
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException($"查询超时（{clampedTimeout} 秒），请缩小查询范围（加 LIMIT 或收窄 MATCH）后重试") { StatusCode = 400 };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || (ex is Neo4jException and not ClientException))
        {
            throw new BusinessException(UnavailableMessage) { StatusCode = 503 };
        }
        catch (ClientException ex)
        {
            throw new BusinessException(ex.Message) { StatusCode = 400 };
        }

        // 托管模式结果侧校验（主防线）：门禁 Contains 可被字符串字面量绕过，须对返回的图元素逐个核对 kgId.
        if (isManaged)
        {
            ValidateManagedRecords(records, knowledgeGraphId);
        }

        var truncated = records.Count > maxRows;
        var effectiveRecords = truncated ? records.Take(maxRows) : records;
        var columns = records.Count > 0 ? records[0].Keys.ToList() : new List<string>();
        var resultRows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var record in effectiveRecords)
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var column in columns)
            {
                row[column] = record.Values.TryGetValue(column, out var value) ? NormalizeCell(value) : null;
            }

            resultRows.Add(row);
        }

        return new KgCypherQueryResult(columns, resultRows, resultRows.Count, truncated);
    }

    /// <inheritdoc/>
    public async Task<KgCypherSchemaDigest> GetSchemaDigestAsync(long knowledgeGraphId, CancellationToken cancellationToken)
    {
        var graph = await GetGraphAsync(knowledgeGraphId, cancellationToken);
        if (string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            return await BuildConnectedDigestAsync(graph, cancellationToken);
        }

        return await BuildManagedDigestAsync(graph, cancellationToken);
    }

    /// <summary>
    /// 查询图谱实体，不存在抛 404.
    /// </summary>
    /// <param name="knowledgeGraphId">图谱 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>图谱实体.</returns>
    private async Task<KnowledgeGraphEntity> GetGraphAsync(long knowledgeGraphId, CancellationToken cancellationToken)
    {
        var graph = await _databaseContext.KnowledgeGraphs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == knowledgeGraphId, cancellationToken);
        if (graph == null)
        {
            throw new BusinessException("图谱不存在.") { StatusCode = 404 };
        }

        return graph;
    }

    /// <summary>
    /// 构建托管图谱摘要：实体/关系类型来自 PG，节点名采样来自图库.
    /// </summary>
    /// <param name="graph">图谱实体.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>托管图摘要.</returns>
    private async Task<KgCypherSchemaDigest> BuildManagedDigestAsync(KnowledgeGraphEntity graph, CancellationToken cancellationToken)
    {
        var entityTypeRows = await _databaseContext.KnowledgeGraphEntityTypes
            .AsNoTracking()
            .Where(x => x.KnowledgeGraphId == graph.Id)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Description, x.Properties })
            .ToListAsync(cancellationToken);
        var relationTypeRows = await _databaseContext.KnowledgeGraphRelationTypes
            .AsNoTracking()
            .Where(x => x.KnowledgeGraphId == graph.Id)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Description, x.SourceTypeId, x.TargetTypeId })
            .ToListAsync(cancellationToken);

        var nameById = entityTypeRows.ToDictionary(x => x.Id, x => x.Name);
        var entityTypes = entityTypeRows
            .Select(x => new KgCypherSchemaItem(x.Name, x.Description, ParsePropertyNames(x.Properties)))
            .ToList();
        var relationTypes = relationTypeRows
            .Select(x => new KgCypherRelationTypeItem(
                x.Name,
                x.SourceTypeId.HasValue ? nameById.GetValueOrDefault(x.SourceTypeId.Value) : null,
                x.TargetTypeId.HasValue ? nameById.GetValueOrDefault(x.TargetTypeId.Value) : null,
                x.Description))
            .ToList();

        // 每类型一条采样查询，消除「单次 LIMIT 扫描前 N 行全属同一类型」的采样偏差.
        var sampleNodes = new List<KgCypherSampleGroup>();
        foreach (var entityType in entityTypeRows)
        {
            var records = await RunInternalQueryAsync(
                null,
                "MATCH (n:KgNode {kgId: $kgId, entityTypeId: $entityTypeId}) RETURN n.name AS name LIMIT 3",
                new { kgId = graph.Id, entityTypeId = entityType.Id },
                cancellationToken);
            var names = new List<string>();
            foreach (var record in records)
            {
                var name = record["name"].As<string>() ?? string.Empty;
                if (!string.IsNullOrEmpty(name) && names.Count < 3)
                {
                    names.Add(name);
                }
            }

            if (names.Count > 0)
            {
                sampleNodes.Add(new KgCypherSampleGroup(entityType.Name, names));
            }
        }

        var (_, dialect) = await _provider.GetRuntimeAsync(cancellationToken);
        return new KgCypherSchemaDigest(graph.Mode, dialect, entityTypes, relationTypes, sampleNodes, Array.Empty<string>(), ManagedUsageText);
    }

    /// <summary>
    /// 构建接入图谱摘要：属性键来自内省缓存，节点名按首标签采样自外部库.
    /// </summary>
    /// <param name="graph">图谱实体.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>接入图摘要.</returns>
    private async Task<KgCypherSchemaDigest> BuildConnectedDigestAsync(KnowledgeGraphEntity graph, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(graph.Database))
        {
            throw new BusinessException("接入图谱缺少数据库配置.") { StatusCode = 409 };
        }

        var (introspection, _, _) = await _introspectionCache.GetAsync(graph.Id, graph.Database, false, cancellationToken);
        var (_, dialect) = await _provider.GetRuntimeAsync(cancellationToken);

        // 外部节点无统一 schema：labels(n) 做分组、name/title/id 启发式取名；
        // 采样为近似结果（单次 LIMIT 100 扫描按首标签分组，类型多或数据倾斜时代表性有限）.
        var records = await RunInternalQueryAsync(
            graph.Database,
            "MATCH (n) RETURN labels(n) AS labels, coalesce(toString(n.name), toString(n.title), toString(n.id), '') AS name LIMIT 100",
            new { },
            cancellationToken);
        var sampleNodes = records
            .Select(record => new
            {
                Labels = NormalizeLabelValues(record["labels"]),
                Name = record["name"].As<string>() ?? string.Empty,
            })
            .Where(item => item.Labels.Count > 0 && !string.IsNullOrEmpty(item.Name))
            .GroupBy(item => item.Labels[0])
            .Take(10)
            .Select(group => new KgCypherSampleGroup(group.Key, group.Take(3).Select(item => item.Name).ToList()))
            .ToList();

        var usage = $"接入图谱（外部 {dialect}）：节点使用原生 label、边使用原生关系类型，无 kgId 属性、查询无需 $kgId 过滤。只读查询，写操作不支持。";
        return new KgCypherSchemaDigest(graph.Mode, dialect, Array.Empty<KgCypherSchemaItem>(), Array.Empty<KgCypherRelationTypeItem>(), sampleNodes, introspection.PropertyKeys, usage);
    }

    /// <summary>
    /// 内部采样查询（不做 $kgId 校验，Cypher 由本服务固定构造）：打开会话只读执行并全量取回.
    /// </summary>
    /// <param name="database">路由数据库（托管图为 null）.</param>
    /// <param name="cypher">内部 Cypher.</param>
    /// <param name="parameters">查询参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>查询记录.</returns>
    private async Task<List<IRecord>> RunInternalQueryAsync(string? database, string cypher, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            var (driver, dialect) = await _provider.GetRuntimeAsync(cancellationToken);
            await using var session = OpenSession(driver, database, dialect);
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                return await cursor.ToListAsync(cancellationToken);
            });
        }

        // 异常映射与 CypherKnowledgeGraphStore 保持同步（勿单方修改词法/路由语义）.
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException("图数据库查询超时，请稍后重试.") { StatusCode = 400 };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || (ex is Neo4jException and not ClientException))
        {
            throw new BusinessException(UnavailableMessage) { StatusCode = 503 };
        }
        catch (ClientException ex)
        {
            throw new BusinessException(ex.Message) { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 托管模式结果侧校验（主防线）：遍历返回记录中的图元素值，kgId 缺失或不等于目标图谱即 403 拒绝.
    /// 堵 $kgId 写进字符串字面量（如 CONTAINS '$kgId'）绕过 Contains 门禁的路径；纯属性投影（如 RETURN n.name）无图元素值，自然跳过.
    /// </summary>
    /// <param name="records">原始查询记录（NormalizeCell 之前）.</param>
    /// <param name="knowledgeGraphId">目标图谱 id.</param>
    private static void ValidateManagedRecords(List<IRecord> records, long knowledgeGraphId)
    {
        foreach (var record in records)
        {
            foreach (var pair in record.Values)
            {
                ValidateManagedCell(pair.Value, knowledgeGraphId, 0);
            }
        }
    }

    /// <summary>
    /// 递归校验单元格值中的图元素（与 NormalizeCell 同构的容器遍历，字典分支在 IEnumerable 之前）.
    /// </summary>
    /// <param name="value">原始单元格值.</param>
    /// <param name="knowledgeGraphId">目标图谱 id.</param>
    /// <param name="depth">当前嵌套深度.</param>
    private static void ValidateManagedCell(object? value, long knowledgeGraphId, int depth)
    {
        if (value == null || depth > MaxCellDepth)
        {
            return;
        }

        switch (value)
        {
            case INode node:
                ValidateManagedEntityKgId(node.Properties, knowledgeGraphId);
                break;
            case IRelationship relationship:
                ValidateManagedEntityKgId(relationship.Properties, knowledgeGraphId);
                break;
            case IPath path:
                foreach (var pathNode in path.Nodes)
                {
                    ValidateManagedEntityKgId(pathNode.Properties, knowledgeGraphId);
                }

                foreach (var pathRelationship in path.Relationships)
                {
                    ValidateManagedEntityKgId(pathRelationship.Properties, knowledgeGraphId);
                }

                break;
            case IDictionary<string, object?> dictionary:
                foreach (var pair in dictionary)
                {
                    ValidateManagedCell(pair.Value, knowledgeGraphId, depth + 1);
                }

                break;
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                foreach (var pair in readOnlyDictionary)
                {
                    ValidateManagedCell(pair.Value, knowledgeGraphId, depth + 1);
                }

                break;
            case IEnumerable<object?> enumerable:
                foreach (var item in enumerable)
                {
                    ValidateManagedCell(item, knowledgeGraphId, depth + 1);
                }

                break;
        }
    }

    /// <summary>
    /// 校验单个图元素的 kgId 属性：缺失、非 long 或不等于目标图谱均视为越界数据.
    /// </summary>
    /// <param name="properties">图元素属性（INode/IRelationship.Properties）.</param>
    /// <param name="knowledgeGraphId">目标图谱 id.</param>
    private static void ValidateManagedEntityKgId(IReadOnlyDictionary<string, object?> properties, long knowledgeGraphId)
    {
        if (!properties.TryGetValue("kgId", out var kgIdValue)
            || kgIdValue is not long kgId
            || kgId != knowledgeGraphId)
        {
            throw new BusinessException(ManagedKgIdMismatchMessage) { StatusCode = 403 };
        }
    }

    /// <summary>
    /// 打开会话；仅 neo4j 方言（外部接入可能使用多数据库）按库名路由，memgraph 恒用默认库.
    /// 与 CypherKnowledgeGraphStore.OpenSession 保持同步（勿单方修改词法/路由语义）.
    /// </summary>
    /// <param name="driver">图数据库驱动.</param>
    /// <param name="database">路由数据库.</param>
    /// <param name="dialect">方言.</param>
    /// <returns>异步会话.</returns>
    private static IAsyncSession OpenSession(IDriver driver, string? database, string dialect)
    {
        var useDatabase = string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(database);
        return useDatabase
            ? driver.AsyncSession(b => b.WithDatabase(database!))
            : driver.AsyncSession();
    }

    /// <summary>
    /// 归一化 labels(n) 结果：Memgraph 对单标签节点返回字符串，多标签返回列表.
    /// </summary>
    /// <param name="value">原始 labels 值.</param>
    /// <returns>标签清单.</returns>
    private static IReadOnlyList<string> NormalizeLabelValues(object? value)
    {
        if (value is string text)
        {
            return new[] { text };
        }

        if (value is IEnumerable<object> list)
        {
            return list.Select(x => x?.ToString() ?? string.Empty).ToList();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// 单元格归一化：图元素降维为普通字典/计数，字符串与容器截断，保证结果可安全序列化.
    /// </summary>
    /// <param name="value">原始单元格值.</param>
    /// <param name="depth">当前嵌套深度（超过 3 层的结构退化为 ToString）.</param>
    /// <returns>归一化后的值.</returns>
    private static object? NormalizeCell(object? value, int depth = 0)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string text)
        {
            return TruncateCellText(text);
        }

        if (value is bool || value is long || value is int || value is double || value is float)
        {
            return value;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        if (depth >= MaxCellDepth)
        {
            return TruncateCellText(value.ToString() ?? string.Empty);
        }

        if (value is INode node)
        {
#pragma warning disable CS0618 // 兜底旧版 Memgraph 不返回 elementId 的场景
            var nodeId = string.IsNullOrEmpty(node.ElementId) ? node.Id.ToString(CultureInfo.InvariantCulture) : node.ElementId;
#pragma warning restore CS0618
            var nodeMap = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["_id"] = nodeId,
                ["_labels"] = node.Labels.ToList(),
            };
            AddCellEntries(nodeMap, node.Properties, depth);
            return nodeMap;
        }

        if (value is IRelationship relationship)
        {
#pragma warning disable CS0618 // 兜底旧版 Memgraph 不返回 elementId 的场景
            var relationshipId = string.IsNullOrEmpty(relationship.ElementId) ? relationship.Id.ToString(CultureInfo.InvariantCulture) : relationship.ElementId;
#pragma warning restore CS0618
            var relationshipMap = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["_id"] = relationshipId,
                ["_type"] = relationship.Type,
            };
            AddCellEntries(relationshipMap, relationship.Properties, depth);
            return relationshipMap;
        }

        if (value is IPath path)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["_pathNodeCount"] = path.Nodes.Count,
                ["_pathRelationshipCount"] = path.Relationships.Count,
            };
        }

        // 字典分支必须在 IEnumerable 之前：字典同样是可枚举类型.
        if (value is IDictionary<string, object?> dictionary)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            AddCellEntries(map, dictionary, depth);
            return map;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            AddCellEntries(map, readOnlyDictionary, depth);
            return map;
        }

        if (value is IEnumerable<object?> enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                if (list.Count >= MaxCellChildren)
                {
                    break;
                }

                list.Add(NormalizeCell(item, depth + 1));
            }

            return list;
        }

        return TruncateCellText(value.ToString() ?? string.Empty);
    }

    /// <summary>
    /// 归一化键值对集合到目标字典（键值递归归一化，条目截断至 50）.
    /// </summary>
    /// <param name="target">目标字典.</param>
    /// <param name="source">源键值对.</param>
    /// <param name="depth">当前嵌套深度.</param>
    private static void AddCellEntries(Dictionary<string, object?> target, IEnumerable<KeyValuePair<string, object?>> source, int depth)
    {
        foreach (var pair in source)
        {
            if (target.Count >= MaxCellChildren)
            {
                break;
            }

            target[pair.Key] = NormalizeCell(pair.Value, depth + 1);
        }
    }

    /// <summary>
    /// 截断超长字符串（超过 2000 字符截断并追加截断标记）.
    /// </summary>
    /// <param name="text">原始字符串.</param>
    /// <returns>截断后的字符串.</returns>
    private static string TruncateCellText(string text)
    {
        return text.Length <= MaxCellLength ? text : string.Concat(text.AsSpan(0, MaxCellLength), CellTruncatedSuffix);
    }

    /// <summary>
    /// 解析实体类型属性定义 JSON（数组，每项含 name 字段），解析失败返回空清单.
    /// </summary>
    /// <param name="propsJson">属性定义 JSON.</param>
    /// <returns>属性名清单.</returns>
    private static IReadOnlyList<string> ParsePropertyNames(string? propsJson)
    {
        if (string.IsNullOrWhiteSpace(propsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(propsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String)
                {
                    names.Add(name.GetString() ?? string.Empty);
                }
            }

            return names;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
