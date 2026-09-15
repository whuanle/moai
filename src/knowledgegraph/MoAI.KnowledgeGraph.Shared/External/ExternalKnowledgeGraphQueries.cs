using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.External;

/// <summary>
/// 查询团队下的知识图谱列表（外部接口）.
/// </summary>
public class QueryExternalGraphsCommand : IRequest<QueryExternalGraphsResponse>, IModelValidator<QueryExternalGraphsCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller 从接入 token 解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalGraphsCommand> validate)
    {
        // Caller 由 Controller 从接入 token 解析填充，自动验证发生在回填之前，因此此处不校验。
    }
}

/// <summary>
/// 图谱列表响应（外部接口）.
/// </summary>
public class QueryExternalGraphsResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyList<ExternalGraphItem> Items { get; init; } = new List<ExternalGraphItem>();
}

/// <summary>
/// 图谱列表项（外部接口）.
/// </summary>
public class ExternalGraphItem
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简介.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 来源：managed（平台托管，可写）/ connected（外部接入，只读）.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;
}

/// <summary>
/// 查询图谱 schema（实体类型 + 关系类型）（外部接口）.
/// </summary>
public class QueryExternalGraphSchemaCommand : IRequest<QueryExternalGraphSchemaCommandResponse>, IModelValidator<QueryExternalGraphSchemaCommand>
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

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalGraphSchemaCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
    }
}

/// <summary>
/// 图谱 schema 响应（外部接口）：仅暴露实体类型与关系类型等业务负载，不包含接入数据库名/内省缓存等内部基础设施字段.
/// </summary>
public class QueryExternalGraphSchemaCommandResponse
{
    /// <summary>
    /// 实体类型.
    /// </summary>
    public List<KnowledgeGraphEntityTypeItem> EntityTypes { get; init; } = new();

    /// <summary>
    /// 关系类型.
    /// </summary>
    public List<KnowledgeGraphRelationTypeItem> RelationTypes { get; init; } = new();
}

/// <summary>
/// 分页查询节点（外部接口）.
/// </summary>
public class QueryExternalNodesCommand : IRequest<QueryKnowledgeGraphNodesCommandResponse>, IModelValidator<QueryExternalNodesCommand>
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
    /// 实体类型筛选.
    /// </summary>
    public long? EntityTypeId { get; init; }

    /// <summary>
    /// 名称关键字.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalNodesCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.PageNo).GreaterThanOrEqualTo(1).WithMessage("页码不正确.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("每页条数不正确（1-200）.");
    }
}

/// <summary>
/// 查询节点详情（外部接口）.
/// </summary>
public class QueryExternalNodeCommand : IRequest<QueryKnowledgeGraphNodeCommandResponse>, IModelValidator<QueryExternalNodeCommand>
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
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalNodeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不能为空.");
    }
}

/// <summary>
/// 一跳邻接展开：返回指定节点的邻居节点与相连的边（外部接口）.
/// </summary>
public class QueryExternalNodeNeighborsCommand : IRequest<QueryKnowledgeGraphCanvasCommandResponse>, IModelValidator<QueryExternalNodeNeighborsCommand>
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
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 邻居数量上限（默认 50，取值 1-500）.
    /// </summary>
    public int Limit { get; init; } = 50;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalNodeNeighborsCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不能为空.");
        validate.RuleFor(x => x.Limit).InclusiveBetween(1, 500).WithMessage("邻居数量上限为 1~500.");
    }
}

/// <summary>
/// 分页查询边（外部接口）.
/// </summary>
public class QueryExternalEdgesCommand : IRequest<QueryKnowledgeGraphEdgesCommandResponse>, IModelValidator<QueryExternalEdgesCommand>
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
    /// 关系类型筛选.
    /// </summary>
    public long? RelationTypeId { get; init; }

    /// <summary>
    /// 端点节点筛选.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalEdgesCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.PageNo).GreaterThanOrEqualTo(1).WithMessage("页码不正确.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 200).WithMessage("每页条数不正确（1-200）.");
    }
}

/// <summary>
/// 查询边详情（外部接口）.
/// </summary>
public class QueryExternalEdgeCommand : IRequest<QueryKnowledgeGraphEdgeCommandResponse>, IModelValidator<QueryExternalEdgeCommand>
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
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalEdgeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不能为空.");
    }
}
