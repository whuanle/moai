using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询节点定义命令（批量查询）.
/// 用于获取指定节点类型的定义信息，包括输入输出字段、参数要求等.
/// 支持批量查询多个节点定义，例如多个插件、知识库或 AI 模型.
/// </summary>
public class QueryNodeDefineCommand : IRequest<QueryNodeDefineCommandResponse>, IModelValidator<QueryNodeDefineCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 节点查询列表.
    /// Key: 节点类型
    /// Value: 节点实例标识列表（插件 ID、知识库 ID、模型 ID 等，转换为字符串）
    /// 对于不需要实例标识的节点类型（如 Start、End、Condition 等），Value 可以为空集合.
    /// </summary>
    public IReadOnlyCollection<KeyValue<NodeType, IReadOnlyCollection<string>>> Nodes { get; init; } = Array.Empty<KeyValue<NodeType, IReadOnlyCollection<string>>>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryNodeDefineCommand> validate)
    {
        validate.RuleFor(x => x.Nodes)
            .NotEmpty().WithMessage("节点查询列表不能为空");

        validate.RuleForEach(x => x.Nodes)
            .ChildRules(node =>
            {
                node.RuleFor(x => x.Key)
                    .IsInEnum().WithMessage("节点类型无效");

                node.RuleFor(x => x.Value)
                    .NotEmpty().WithMessage("Plugin、Wiki、AiChat 节点必须提供实例标识列表")
                    .When(x => x.Key == NodeType.Plugin || x.Key == NodeType.Wiki || x.Key == NodeType.AiChat);
            });
    }
}
