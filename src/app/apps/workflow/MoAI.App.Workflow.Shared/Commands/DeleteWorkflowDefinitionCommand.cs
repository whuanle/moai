using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 删除工作流定义命令.
/// 执行软删除操作，保留工作流定义和执行历史以供审计.
/// </summary>
public class DeleteWorkflowDefinitionCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWorkflowDefinitionCommand>
{
    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteWorkflowDefinitionCommand> validate)
    {
        validate.RuleFor(x => x.Id)
            .NotEmpty().WithMessage("工作流定义 ID 不能为空");
    }
}
