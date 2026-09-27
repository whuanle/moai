using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.External;

/// <summary>
/// 批量导入节点与边（外部接口）：面向 CSV/JSON 等外部数据源解析后的结构化写入。
/// 类型按名称引用（可自动创建）、节点携带业务 key 幂等 upsert、边端点按 key/名称/节点 id 引用、逐条返回结果（坏行不阻断整批）.
/// </summary>
public class ImportExternalGraphDataCommand : IRequest<ExternalImportResponse>, IModelValidator<ImportExternalGraphDataCommand>
{
    /// <summary>
    /// upsert 模式：按 key（或类型+名称）匹配现有节点做更新，边按（起点,关系,终点）去重.
    /// </summary>
    public const string ModeUpsert = "upsert";

    /// <summary>
    /// create 模式：一律新建（不做匹配与去重）.
    /// </summary>
    public const string ModeCreate = "create";

    /// <summary>
    /// 单次导入节点数上限.
    /// </summary>
    public const int MaxNodes = 500;

    /// <summary>
    /// 单次导入边数上限.
    /// </summary>
    public const int MaxEdges = 500;

    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 写入模式：upsert（默认，幂等重导）或 create（一律新建）.
    /// </summary>
    public string Mode { get; init; } = ModeUpsert;

    /// <summary>
    /// 实体/关系类型名称不存在时是否自动创建（实体类型属性定义取该类型各行属性键并集，均为 string；关系类型约束为任意）.
    /// </summary>
    public bool AutoCreateTypes { get; init; }

    /// <summary>
    /// 仅校验不落库：完整走一遍类型解析/节点匹配/端点解析/约束校验，预测 created/updated/skipped/failed 与逐条结果，但不建类型、不写图库、不发向量增量；created 行 id 返回 null.
    /// </summary>
    public bool ValidateOnly { get; init; }

    /// <summary>
    /// 是否做疑似重复实体检测（向量相似度，需图谱已配置向量化模型）；默认开启，检测失败自动跳过不影响导入.
    /// </summary>
    public bool DetectDuplicates { get; init; } = true;

    /// <summary>
    /// 节点列表.
    /// </summary>
    public IReadOnlyList<ExternalImportNodeItem> Nodes { get; init; } = new List<ExternalImportNodeItem>();

    /// <summary>
    /// 边列表；端点可引用本请求内的节点，也可引用此前导入的节点.
    /// </summary>
    public IReadOnlyList<ExternalImportEdgeItem> Edges { get; init; } = new List<ExternalImportEdgeItem>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportExternalGraphDataCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Mode).Must(x => x == ModeUpsert || x == ModeCreate).WithMessage("mode 仅支持 upsert 或 create.");
        validate.RuleFor(x => x.Nodes).Must(x => x.Count <= MaxNodes).WithMessage($"单次导入节点最多 {MaxNodes} 条.");
        validate.RuleFor(x => x.Edges).Must(x => x.Count <= MaxEdges).WithMessage($"单次导入边最多 {MaxEdges} 条.");
        validate.RuleFor(x => new { x.Nodes, x.Edges }).Must(x => x.Nodes.Count + x.Edges.Count > 0).WithMessage("nodes 与 edges 不能同时为空.");

        validate.RuleForEach(x => x.Nodes).ChildRules(item =>
        {
            item.RuleFor(i => i.Key).MaximumLength(200).WithMessage("key 最长 200 个字符.");
            item.RuleFor(i => i.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
            item.RuleFor(i => i.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
            item.RuleFor(i => i.EntityTypeId).Must(x => x == null || x > 0).WithMessage("实体类型 id 不正确.");
            item.RuleFor(i => i.EntityTypeName).MaximumLength(50).WithMessage("实体类型名称最长 50 个字符.");
            item.RuleFor(i => i).Must(i => i.EntityTypeId != null || !string.IsNullOrWhiteSpace(i.EntityTypeName)).WithMessage("entityTypeId 与 entityTypeName 必填其一.");
            item.RuleFor(i => i.Properties).Must(x => x == null || x.Count <= 50).WithMessage("属性最多 50 个.");
            item.RuleFor(i => i.Properties).Must(x => x == null || x.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= 100)).WithMessage("属性名不能为空且最长 100 个字符.");
            item.RuleFor(i => i.Properties).Must(x => x == null || x.Values.All(v => v == null || v.Length <= 2000)).WithMessage("属性值最长 2000 个字符.");
        });

        validate.RuleForEach(x => x.Edges).ChildRules(item =>
        {
            item.RuleFor(i => i.RelationTypeId).Must(x => x == null || x > 0).WithMessage("关系类型 id 不正确.");
            item.RuleFor(i => i.RelationTypeName).MaximumLength(50).WithMessage("关系类型名称最长 50 个字符.");
            item.RuleFor(i => i).Must(i => i.RelationTypeId != null || !string.IsNullOrWhiteSpace(i.RelationTypeName)).WithMessage("relationTypeId 与 relationTypeName 必填其一.");
            item.RuleFor(i => i.Source).NotNull().WithMessage("source 不能为空.").Must(ExternalImportNodeRef.HasAnyRef).WithMessage("source 必须提供 nodeId、key、name 之一.");
            item.RuleFor(i => i.Target).NotNull().WithMessage("target 不能为空.").Must(ExternalImportNodeRef.HasAnyRef).WithMessage("target 必须提供 nodeId、key、name 之一.");
        });
    }
}

/// <summary>
/// 导入节点行.
/// </summary>
public class ExternalImportNodeItem
{
    /// <summary>
    /// 业务幂等键（可选）：upsert 模式下按此匹配现有节点，未命中时回退按（实体类型+名称）匹配并收养该 key；未提供 key 时按（实体类型+名称）匹配.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// 实体类型 id，与 entityTypeName 二选一（id 优先）.
    /// </summary>
    public long? EntityTypeId { get; init; }

    /// <summary>
    /// 实体类型名称，与 entityTypeId 二选一.
    /// </summary>
    public string? EntityTypeName { get; init; }

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 实例属性值（键值对均以字符串存储；不要求在实体类型属性定义内，超出定义部分的键暂不在画布渲染）.
    /// </summary>
    public Dictionary<string, string>? Properties { get; init; }
}

/// <summary>
/// 导入边行.
/// </summary>
public class ExternalImportEdgeItem
{
    /// <summary>
    /// 关系类型 id，与 relationTypeName 二选一（id 优先）.
    /// </summary>
    public long? RelationTypeId { get; init; }

    /// <summary>
    /// 关系类型名称，与 relationTypeId 二选一.
    /// </summary>
    public string? RelationTypeName { get; init; }

    /// <summary>
    /// 起点节点引用.
    /// </summary>
    public ExternalImportNodeRef Source { get; init; } = default!;

    /// <summary>
    /// 终点节点引用.
    /// </summary>
    public ExternalImportNodeRef Target { get; init; } = default!;
}

/// <summary>
/// 边端点节点引用：nodeId、key、name 至少提供一个；同时提供时按 nodeId &gt; key &gt; name 优先解析.
/// </summary>
public class ExternalImportNodeRef
{
    /// <summary>
    /// 图库节点 id.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 业务幂等键.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// 节点名称；同一名称命中多个节点时报错，需以 entityTypeName 消歧.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 实体类型名称（name 引用的可选消歧）.
    /// </summary>
    public string? EntityTypeName { get; init; }

    /// <summary>
    /// 是否携带至少一种节点引用（nodeId/key/name）.
    /// </summary>
    public static bool HasAnyRef(ExternalImportNodeRef? nodeRef)
        => nodeRef != null && (!string.IsNullOrWhiteSpace(nodeRef.NodeId) || !string.IsNullOrWhiteSpace(nodeRef.Key) || !string.IsNullOrWhiteSpace(nodeRef.Name));
}

/// <summary>
/// 批量导入响应（外部接口）：逐条返回结果，坏行不阻断整批，HTTP 200.
/// </summary>
public class ExternalImportResponse
{
    /// <summary>
    /// 新建节点数.
    /// </summary>
    public int NodeCreatedCount { get; init; }

    /// <summary>
    /// 更新节点数.
    /// </summary>
    public int NodeUpdatedCount { get; init; }

    /// <summary>
    /// 失败节点数.
    /// </summary>
    public int NodeFailedCount { get; init; }

    /// <summary>
    /// 新建边数.
    /// </summary>
    public int EdgeCreatedCount { get; init; }

    /// <summary>
    /// 跳过边数（已存在的同（起点，关系，终点）边，仅 upsert 模式）.
    /// </summary>
    public int EdgeSkippedCount { get; init; }

    /// <summary>
    /// 失败边数.
    /// </summary>
    public int EdgeFailedCount { get; init; }

    /// <summary>
    /// 本次（validateOnly 模式下为「将」）自动创建的实体类型名称.
    /// </summary>
    public IReadOnlyList<string> CreatedEntityTypeNames { get; init; } = new List<string>();

    /// <summary>
    /// 本次（validateOnly 模式下为「将」）自动创建的关系类型名称.
    /// </summary>
    public IReadOnlyList<string> CreatedRelationTypeNames { get; init; } = new List<string>();

    /// <summary>
    /// 逐条结果：先节点（按请求顺序）后边（按请求顺序）；validateOnly 模式下为预测值，id 恒为 null.
    /// </summary>
    public IReadOnlyList<ExternalImportItemResult> Results { get; init; } = new List<ExternalImportItemResult>();

    /// <summary>
    /// 疑似重复实体对（向量相似度检测，detectDuplicates 开启且图谱已配置向量化时才有值）；检测失败不影响导入.
    /// </summary>
    public IReadOnlyList<ImportDuplicateSuspect> DuplicateSuspects { get; init; } = new List<ImportDuplicateSuspect>();
}

/// <summary>
/// 导入疑似重复项：新建节点与图谱已有节点（kind=existing）或本批次另一新建行（kind=inbatch）向量相似度超阈值.
/// </summary>
public class ImportDuplicateSuspect
{
    /// <summary>
    /// 新建节点在请求中的行下标（从 0 开始）.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 新建节点名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 匹配来源：existing（图谱已有节点）/ inbatch（本批次另一行）.
    /// </summary>
    public string Kind { get; init; } = "existing";

    /// <summary>
    /// 相似度得分（Cosine，0-1）.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// 已有节点 id（kind=existing 时有值）.
    /// </summary>
    public string? MatchNodeId { get; init; }

    /// <summary>
    /// 本批次另一行的行下标（kind=inbatch 时有值）.
    /// </summary>
    public int? MatchIndex { get; init; }

    /// <summary>
    /// 匹配节点名称.
    /// </summary>
    public string MatchName { get; init; } = default!;
}

/// <summary>
/// 导入逐条结果.
/// </summary>
public class ExternalImportItemResult
{
    /// <summary>
    /// 行类型：node / edge.
    /// </summary>
    public string Kind { get; init; } = "node";

    /// <summary>
    /// 请求中的行下标（从 0 开始，节点与边各自独立计数）.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 是否成功.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// 节点/边 id（成功时有值）.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// 动作：created / updated / skipped.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// 失败原因（ok=false 时有值）.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// 按业务 key 批量删除节点（外部接口）：连带其边，用于同步场景清理源数据中已移除的行.
/// </summary>
public class DeleteExternalNodesByKeysCommand : IRequest<DeleteExternalNodesByKeysResponse>, IModelValidator<DeleteExternalNodesByKeysCommand>
{
    /// <summary>
    /// 单次删除 key 数上限.
    /// </summary>
    public const int MaxKeys = 500;

    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 业务 key 列表.
    /// </summary>
    public IReadOnlyList<string> Keys { get; init; } = new List<string>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteExternalNodesByKeysCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Keys).NotEmpty().WithMessage("keys 不能为空.").Must(x => x.Count <= MaxKeys).WithMessage($"单次最多删除 {MaxKeys} 条.");
        validate.RuleForEach(x => x.Keys).NotEmpty().WithMessage("key 不能为空.").MaximumLength(200).WithMessage("key 最长 200 个字符.");
    }
}

/// <summary>
/// 按 key 批量删除节点响应（外部接口）.
/// </summary>
public class DeleteExternalNodesByKeysResponse
{
    /// <summary>
    /// 实际删除的节点数（不存在的 key 忽略）.
    /// </summary>
    public int DeletedCount { get; init; }
}

/// <summary>
/// 按业务 key 查询节点详情（外部接口），用于同步场景核对导入结果.
/// </summary>
public class QueryExternalNodeByKeyCommand : IRequest<QueryKnowledgeGraphNodeCommandResponse>, IModelValidator<QueryExternalNodeByKeyCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 业务 key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalNodeByKeyCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("key 不能为空.").MaximumLength(200).WithMessage("key 最长 200 个字符.");
    }
}

/// <summary>
/// 分页枚举图内已落业务 key 的节点（外部接口）：同步场景全量比对（找出源数据已移除、图中仍存在的 key）.
/// </summary>
public class QueryExternalNodeKeysCommand : IRequest<QueryExternalNodeKeysResponse>, IModelValidator<QueryExternalNodeKeysCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 100;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalNodeKeysCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.PageNo).GreaterThanOrEqualTo(1).WithMessage("页码不正确.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 500).WithMessage("每页条数不正确（1-500）.");
    }
}

/// <summary>
/// 业务 key 枚举响应（外部接口）.
/// </summary>
public class QueryExternalNodeKeysResponse
{
    /// <summary>
    /// 当前页.
    /// </summary>
    public IReadOnlyList<ExternalNodeKeyItem> Items { get; init; } = new List<ExternalNodeKeyItem>();
}

/// <summary>
/// 业务 key 枚举项（外部接口）.
/// </summary>
public class ExternalNodeKeyItem
{
    /// <summary>
    /// 业务 key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 图库节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }
}

/// <summary>
/// 按端点引用批量删除边（外部接口）：同步场景清理源数据中已移除的关系；建议先删边后删节点（节点删除会 DETACH 级联其边）.
/// </summary>
public class DeleteExternalEdgesByRefsCommand : IRequest<DeleteExternalEdgesByRefsResponse>, IModelValidator<DeleteExternalEdgesByRefsCommand>
{
    /// <summary>
    /// 单次删除行数上限.
    /// </summary>
    public const int MaxItems = 500;

    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 待删边列表（端点不存在或关系类型不存在按行失败；端点存在但无边为幂等成功，计 0）.
    /// </summary>
    public IReadOnlyList<ExternalEdgeRefItem> Items { get; init; } = new List<ExternalEdgeRefItem>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteExternalEdgesByRefsCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Items).NotEmpty().WithMessage("items 不能为空.").Must(x => x.Count <= MaxItems).WithMessage($"单次最多 {MaxItems} 条.");
        validate.RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.RelationTypeId).Must(x => x == null || x > 0).WithMessage("关系类型 id 不正确.");
            item.RuleFor(i => i.RelationTypeName).MaximumLength(50).WithMessage("关系类型名称最长 50 个字符.");
            item.RuleFor(i => i).Must(i => i.RelationTypeId != null || !string.IsNullOrWhiteSpace(i.RelationTypeName)).WithMessage("relationTypeId 与 relationTypeName 必填其一.");
            item.RuleFor(i => i.Source).NotNull().WithMessage("source 不能为空.").Must(ExternalImportNodeRef.HasAnyRef).WithMessage("source 必须提供 nodeId、key、name 之一.");
            item.RuleFor(i => i.Target).NotNull().WithMessage("target 不能为空.").Must(ExternalImportNodeRef.HasAnyRef).WithMessage("target 必须提供 nodeId、key、name 之一.");
        });
    }
}

/// <summary>
/// 按端点引用删除边行.
/// </summary>
public class ExternalEdgeRefItem
{
    /// <summary>
    /// 关系类型 id，与 relationTypeName 二选一（id 优先）.
    /// </summary>
    public long? RelationTypeId { get; init; }

    /// <summary>
    /// 关系类型名称，与 relationTypeId 二选一.
    /// </summary>
    public string? RelationTypeName { get; init; }

    /// <summary>
    /// 起点节点引用.
    /// </summary>
    public ExternalImportNodeRef Source { get; init; } = default!;

    /// <summary>
    /// 终点节点引用.
    /// </summary>
    public ExternalImportNodeRef Target { get; init; } = default!;
}

/// <summary>
/// 按端点引用批量删除边响应（外部接口）.
/// </summary>
public class DeleteExternalEdgesByRefsResponse
{
    /// <summary>
    /// 实际删除的边数（同一三元组的多条平行边一并删除）.
    /// </summary>
    public int DeletedCount { get; init; }

    /// <summary>
    /// 逐行结果（按请求顺序）；ok 的行代表该行解析成功并执行删除（删除 0 条也为 ok），失败行附原因.
    /// </summary>
    public IReadOnlyList<ExternalEdgeDeleteItemResult> Results { get; init; } = new List<ExternalEdgeDeleteItemResult>();
}

/// <summary>
/// 按端点引用删除边逐行结果（外部接口）.
/// </summary>
public class ExternalEdgeDeleteItemResult
{
    /// <summary>
    /// 请求中的行下标（从 0 开始）.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 是否成功（端点/关系类型未命中为失败）.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// 失败原因（ok=false 时有值）.
    /// </summary>
    public string? Message { get; init; }
}
