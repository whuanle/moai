using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.External;

/// <summary>
/// 外部调用方身份（应用 token 解析后由 Controller 填充）.
/// </summary>
public class ExternalGraphCaller
{
    /// <summary>
    /// 归属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 来源应用接入 id.
    /// </summary>
    public Guid AccessAppId { get; init; }
}

/// <summary>
/// 新增实体类型（外部接口）.
/// </summary>
public class CreateExternalEntityTypeCommand : IRequest<SimpleLong>, IModelValidator<CreateExternalEntityTypeCommand>
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
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 属性定义.
    /// </summary>
    public List<KnowledgeGraphEntityTypeProperty> Properties { get; init; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalEntityTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("类型名称不能为空.").MaximumLength(50).WithMessage("类型名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        EntityTypePropertyRules.Apply(validate.RuleFor(x => x.Properties));
    }
}

/// <summary>
/// 修改实体类型（外部接口）.
/// </summary>
public class UpdateExternalEntityTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateExternalEntityTypeCommand>
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
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 属性定义.
    /// </summary>
    public List<KnowledgeGraphEntityTypeProperty> Properties { get; init; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateExternalEntityTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // EntityTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("类型名称不能为空.").MaximumLength(50).WithMessage("类型名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        EntityTypePropertyRules.Apply(validate.RuleFor(x => x.Properties));
    }
}

/// <summary>
/// 删除实体类型（外部接口）.
/// </summary>
public class DeleteExternalEntityTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteExternalEntityTypeCommand>
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
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteExternalEntityTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
    }
}

/// <summary>
/// 新增关系类型（外部接口）.
/// </summary>
public class CreateExternalRelationTypeCommand : IRequest<SimpleLong>, IModelValidator<CreateExternalRelationTypeCommand>
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
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalRelationTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("关系名称不能为空.").MaximumLength(50).WithMessage("关系名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}

/// <summary>
/// 修改关系类型（外部接口）.
/// </summary>
public class UpdateExternalRelationTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateExternalRelationTypeCommand>
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
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateExternalRelationTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // RelationTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("关系名称不能为空.").MaximumLength(50).WithMessage("关系名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}

/// <summary>
/// 删除关系类型（外部接口）.
/// </summary>
public class DeleteExternalRelationTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteExternalRelationTypeCommand>
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
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteExternalRelationTypeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
    }
}

/// <summary>
/// 新增节点（外部接口）.
/// </summary>
public class CreateExternalNodeCommand : IRequest<SimpleString>, IModelValidator<CreateExternalNodeCommand>
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
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 实例属性值（键为实体类型定义的属性名，值以字符串存储）.
    /// </summary>
    public Dictionary<string, string>? Properties { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalNodeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Count <= 50).WithMessage("属性最多 50 个.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= 100)).WithMessage("属性名不能为空且最长 100 个字符.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Values.All(v => v == null || v.Length <= 2000)).WithMessage("属性值最长 2000 个字符.");
    }
}

/// <summary>
/// 修改节点（外部接口）.
/// </summary>
public class UpdateExternalNodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateExternalNodeCommand>
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
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 实例属性值（键为实体类型定义的属性名，值以字符串存储）.
    /// </summary>
    public Dictionary<string, string>? Properties { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateExternalNodeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // NodeId/EntityTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验。
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Count <= 50).WithMessage("属性最多 50 个.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= 100)).WithMessage("属性名不能为空且最长 100 个字符.");
        validate.RuleFor(x => x.Properties).Must(x => x == null || x.Values.All(v => v == null || v.Length <= 2000)).WithMessage("属性值最长 2000 个字符.");
    }
}

/// <summary>
/// 删除节点（连带其边）（外部接口）.
/// </summary>
public class DeleteExternalNodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteExternalNodeCommand>
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
    public static void Validate(AbstractValidator<DeleteExternalNodeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不正确.");
    }
}

/// <summary>
/// 新增边（外部接口）.
/// </summary>
public class CreateExternalEdgeCommand : IRequest<SimpleString>, IModelValidator<CreateExternalEdgeCommand>
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
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = default!;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalEdgeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
        validate.RuleFor(x => x.SourceNodeId).NotEmpty().WithMessage("起点节点不正确.");
        validate.RuleFor(x => x.TargetNodeId).NotEmpty().WithMessage("终点节点不正确.");
    }
}

/// <summary>
/// 修改边（外部接口）.
/// </summary>
public class UpdateExternalEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateExternalEdgeCommand>
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

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateExternalEdgeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // EdgeId/RelationTypeId 为路由字段回填，自动校验发生在回填之前，不在此校验。
    }
}

/// <summary>
/// 删除边（外部接口）.
/// </summary>
public class DeleteExternalEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteExternalEdgeCommand>
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
    public static void Validate(AbstractValidator<DeleteExternalEdgeCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不正确.");
    }
}

/// <summary>
/// 批量新增节点（外部接口）.
/// </summary>
public class CreateExternalNodesBatchCommand : IRequest<ExternalBatchResponse>, IModelValidator<CreateExternalNodesBatchCommand>
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
    /// 节点列表.
    /// </summary>
    public IReadOnlyList<ExternalNodeInput> Items { get; init; } = new List<ExternalNodeInput>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalNodesBatchCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Items).NotEmpty().WithMessage("节点列表不能为空.").Must(x => x.Count <= 200).WithMessage("单批次最多 200 条.");
        validate.RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
            item.RuleFor(i => i.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
            item.RuleFor(i => i.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
        });
    }
}

/// <summary>
/// 批量新增节点项.
/// </summary>
public class ExternalNodeInput
{
    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// 批量新增边（外部接口）.
/// </summary>
public class CreateExternalEdgesBatchCommand : IRequest<ExternalBatchResponse>, IModelValidator<CreateExternalEdgesBatchCommand>
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
    /// 边列表.
    /// </summary>
    public IReadOnlyList<ExternalEdgeInput> Items { get; init; } = new List<ExternalEdgeInput>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateExternalEdgesBatchCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Items).NotEmpty().WithMessage("边列表不能为空.").Must(x => x.Count <= 200).WithMessage("单批次最多 200 条.");
        validate.RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
            item.RuleFor(i => i.SourceNodeId).NotEmpty().WithMessage("起点节点不正确.");
            item.RuleFor(i => i.TargetNodeId).NotEmpty().WithMessage("终点节点不正确.");
        });
    }
}

/// <summary>
/// 批量新增边项.
/// </summary>
public class ExternalEdgeInput
{
    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = default!;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = default!;
}

/// <summary>
/// 批量写入响应（外部接口）.
/// </summary>
public class ExternalBatchResponse
{
    /// <summary>
    /// 成功数量.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// 失败数量.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// 逐条结果：包含全部行（成功与失败），按请求顺序排列，数量与请求 Items 一致.
    /// </summary>
    public IReadOnlyList<ExternalBatchItemResult> Results { get; init; } = new List<ExternalBatchItemResult>();
}

/// <summary>
/// 批量写入逐条结果（外部接口）.
/// </summary>
public class ExternalBatchItemResult
{
    /// <summary>
    /// 请求中的行下标（从 0 开始）.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 是否成功.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// 创建的节点/边 id（成功时有值，失败时为 null）.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// 失败原因（成功时为 null）.
    /// </summary>
    public string? Message { get; init; }
}
