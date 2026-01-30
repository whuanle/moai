using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 节点设计定义.
/// </summary>
public class NodeDesignfinition : IModelValidator<NodeDesignfinition>
{
    /// <summary>
    /// 节点设计列表.
    /// 包含工作流中所有节点的配置信息.
    /// 节点之间的连接关系通过 NodeDesign.NextNodeKeys 定义.
    /// </summary>
    public IReadOnlyCollection<NodeDesign> Nodes { get; set; } = Array.Empty<NodeDesign>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<NodeDesignfinition> validate)
    {
        validate.RuleFor(x => x.Nodes)
            .NotEmpty().WithMessage("节点设计列表不能为空");
    }
}