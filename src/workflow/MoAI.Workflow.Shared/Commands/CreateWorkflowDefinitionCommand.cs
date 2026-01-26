using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 创建工作流定义命令.
/// 用于创建新的工作流定义，只包含基础信息（名称、描述、头像等）.
/// 创建后可以通过更新命令编辑设计草稿，通过发布命令发布正式版本.
/// </summary>
public class CreateWorkflowDefinitionCommand : IRequest<SimpleGuid>, IModelValidator<CreateWorkflowDefinitionCommand>
{
    /// <summary>
    /// 团队 ID，工作流归属的团队.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 工作流名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工作流描述信息.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 工作流头像 ObjectKey.
    /// </summary>
    public string? Avatar { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateWorkflowDefinitionCommand> validate)
    {
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("工作流名称不能为空")
            .MaximumLength(100).WithMessage("工作流名称不能超过100个字符");

        validate.RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("工作流描述不能超过500个字符")
            .When(x => !string.IsNullOrEmpty(x.Description));

        validate.RuleFor(x => x.Avatar)
            .MaximumLength(200).WithMessage("头像路径不能超过200个字符")
            .When(x => !string.IsNullOrEmpty(x.Avatar));
    }
}
