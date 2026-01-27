using FluentValidation;
using MediatR;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 批量查询节点定义命令（聚合 API）.
/// 用于一次性获取多个节点类型的定义信息，减少 API 调用次数.
/// 支持混合查询不同类型的节点，包括需要额外参数的 Plugin、AiChat、Wiki 节点.
/// </summary>
public class QueryBatchNodeDefineCommand : IRequest<QueryBatchNodeDefineCommandResponse>, IModelValidator<QueryBatchNodeDefineCommand>
{
    /// <summary>
    /// 节点定义请求列表.
    /// </summary>
    public IReadOnlyList<NodeDefineRequest> Requests { get; init; } = Array.Empty<NodeDefineRequest>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryBatchNodeDefineCommand> validate)
    {
        validate.RuleFor(x => x.Requests)
            .NotEmpty().WithMessage("请求列表不能为空")
            .Must(x => x.Count <= 50).WithMessage("单次最多查询 50 个节点定义");

        validate.RuleForEach(x => x.Requests)
            .SetValidator(new NodeDefineRequestValidator());
    }
}

/// <summary>
/// 节点定义请求项.
/// </summary>
public class NodeDefineRequest
{
    /// <summary>
    /// 节点类型.
    /// </summary>
    public NodeType NodeType { get; init; }

    /// <summary>
    /// 插件 ID（仅当 NodeType 为 Plugin 时需要）.
    /// </summary>
    public int? PluginId { get; init; }

    /// <summary>
    /// AI 模型 ID（仅当 NodeType 为 AiChat 时可选）.
    /// </summary>
    public int? ModelId { get; init; }

    /// <summary>
    /// 知识库 ID（仅当 NodeType 为 Wiki 时可选）.
    /// </summary>
    public int? WikiId { get; init; }

    /// <summary>
    /// 请求标识符（可选，用于客户端关联请求和响应）.
    /// </summary>
    public string? RequestId { get; init; }
}

/// <summary>
/// 节点定义请求验证器.
/// </summary>
public class NodeDefineRequestValidator : AbstractValidator<NodeDefineRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodeDefineRequestValidator"/> class.
    /// </summary>
    public NodeDefineRequestValidator()
    {
        RuleFor(x => x.NodeType)
            .IsInEnum().WithMessage("节点类型无效");

        RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("Plugin 节点必须提供 PluginId")
            .When(x => x.NodeType == NodeType.Plugin);
    }
}
