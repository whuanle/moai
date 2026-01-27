using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 更新工作流定义命令.
/// 用于更新工作流的基础信息和设计草稿.
/// 基础信息（Name、Description、Avatar）保存在 AppEntity 中.
/// 设计草稿保存在 AppWorkflowDesignEntity 的 UiDesignDraft 和 FunctionDesignDraft 字段中，不会影响已发布的版本.
/// 更新时会验证工作流定义的有效性（节点类型、连接等）.
/// 需要通过发布命令将草稿发布为正式版本.
/// </summary>
public class UpdateWorkflowDefinitionCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWorkflowDefinitionCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用 ID.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 工作流名称）.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 工作流描述信息.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 工作流头像 ObjectKey.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// UI 设计草稿，用于前端可视化设计器.
    /// 保存草稿不会影响已发布的版本.
    /// </summary>
    public UiDesign? UiDesignDraft { get; set; }

    /// <summary>
    /// 节点设计列表.
    /// 包含工作流中所有节点的配置信息.
    /// </summary>
    public IReadOnlyCollection<NodeDesign>? Nodes { get; set; }

    /// <summary>
    /// 连接列表.
    /// 定义节点之间的连接关系.
    /// </summary>
    public IReadOnlyCollection<Connection>? Connections { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWorkflowDefinitionCommand> validate)
    {
        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用 ID 不能为空");

        validate.RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("工作流名称不能超过100个字符")
            .When(x => !string.IsNullOrEmpty(x.Name));

        validate.RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("工作流描述不能超过500个字符")
            .When(x => !string.IsNullOrEmpty(x.Description));

        validate.RuleFor(x => x.Avatar)
            .MaximumLength(200).WithMessage("头像路径不能超过200个字符")
            .When(x => !string.IsNullOrEmpty(x.Avatar));
    }
}
