using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.External;

namespace MoAI.KnowledgeGraph.External;

/// <summary>
/// 知识图谱 MCP 向量召回（外部接口，只读）：在指定托管图内按语义召回与查询最相关的实体及其一跳邻居.
/// </summary>
public class QueryExternalKgRecallCommand : IRequest<QueryExternalKgRecallCommandResponse>, IModelValidator<QueryExternalKgRecallCommand>
{
    /// <summary>
    /// 外部调用方身份，由 Controller/工具从接入凭证解析填充.
    /// </summary>
    [JsonIgnore]
    public ExternalGraphCaller Caller { get; init; } = default!;

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 查询文本.
    /// </summary>
    public string Query { get; init; } = default!;

    /// <summary>
    /// 返回条数（1-50）.
    /// </summary>
    public int Top { get; init; } = 5;

    /// <summary>
    /// 相似度阈值（0-1），低于阈值的命中项被丢弃；null 不过滤.
    /// </summary>
    public double? MinScore { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryExternalKgRecallCommand> validate)
    {
        // KnowledgeGraphId 由调用方回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Query).NotEmpty().WithMessage("请输入查询文本.").MaximumLength(1000).WithMessage("查询文本最长 1000 个字符.");
        validate.RuleFor(x => x.Top).InclusiveBetween(1, 50).WithMessage("返回条数为 1-50.");
        validate.RuleFor(x => x.MinScore).InclusiveBetween(0, 1).WithMessage("相似度阈值必须在 0-1 之间.").When(x => x.MinScore != null);
    }
}

/// <summary>
/// 知识图谱向量召回响应（外部接口）.
/// </summary>
public class QueryExternalKgRecallCommandResponse
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 图谱名称.
    /// </summary>
    public string KnowledgeGraphName { get; init; } = string.Empty;

    /// <summary>
    /// 查询文本.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 命中实体（按得分降序）.
    /// </summary>
    public List<QueryExternalKgRecallItem> Items { get; init; } = new();

    /// <summary>
    /// 未执行召回的原因说明（图谱未配置向量化/模型不可用等），正常召回为空.
    /// </summary>
    public string? SkippedHint { get; init; }
}

/// <summary>
/// 知识图谱向量召回命中项（外部接口）.
/// </summary>
public class QueryExternalKgRecallItem
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 节点描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型名称（类型未定义时为 null）.
    /// </summary>
    public string? EntityTypeName { get; init; }

    /// <summary>
    /// 相似度得分（Cosine）.
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// 一跳邻居摘要.
    /// </summary>
    public List<KgRecallNeighbor> Neighbors { get; init; } = new();
}

/// <summary>
/// 召回命中实体的一跳邻居摘要（外部接口）.
/// </summary>
public class KgRecallNeighbor
{
    /// <summary>
    /// 关系类型名（类型未定义时为 null）.
    /// </summary>
    public string? RelationName { get; init; }

    /// <summary>
    /// 方向：out（出边）/ in（入边）.
    /// </summary>
    public string Direction { get; init; } = default!;

    /// <summary>
    /// 邻居节点名.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 邻居描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
