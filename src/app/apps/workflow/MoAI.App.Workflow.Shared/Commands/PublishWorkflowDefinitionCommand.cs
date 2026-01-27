using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 发布工作流定义命令.
/// 将草稿版本（UiDesignDraft、FunctionDesignDraft）发布为正式版本（UiDesign、FunctionDesign）.
/// 发布时会创建版本快照（WorkflowDesignSnapshotEntity）以维护历史记录.
/// 发布后会验证工作流定义的有效性（节点类型、连接等）.
/// </summary>
public class PublishWorkflowDefinitionCommand : IRequest<EmptyCommandResponse>, IModelValidator<PublishWorkflowDefinitionCommand>
{
    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PublishWorkflowDefinitionCommand> validate)
    {
        validate.RuleFor(x => x.Id)
            .NotEmpty().WithMessage("工作流定义 ID 不能为空");
    }
}
