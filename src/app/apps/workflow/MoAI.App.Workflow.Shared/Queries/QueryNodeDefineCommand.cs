using FluentValidation;
using MediatR;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询单个节点定义命令.
/// 用于获取指定节点类型的定义信息，包括输入输出字段、参数要求等.
/// 对于 Plugin 节点，需要提供 PluginId 以获取特定插件的定义.
/// </summary>
public class QueryNodeDefineCommand : IRequest<QueryNodeDefineCommandResponse>, IModelValidator<QueryNodeDefineCommand>
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
    /// AI 模型 ID（仅当 NodeType 为 AiChat 时可选，用于获取特定模型的配置）.
    /// </summary>
    public int? ModelId { get; init; }

    /// <summary>
    /// 知识库 ID（仅当 NodeType 为 Wiki 时可选，用于获取特定知识库的配置）.
    /// </summary>
    public int? WikiId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryNodeDefineCommand> validate)
    {
        validate.RuleFor(x => x.NodeType)
            .IsInEnum().WithMessage("节点类型无效");

        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("Plugin 节点必须提供 PluginId")
            .When(x => x.NodeType == NodeType.Plugin);
    }
}
