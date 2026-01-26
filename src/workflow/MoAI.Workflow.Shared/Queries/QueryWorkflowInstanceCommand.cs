using FluentValidation;
using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询工作流实例命令.
/// 用于检索单个工作流实例的执行信息，包括执行状态、参数和结果.
/// </summary>
public class QueryWorkflowInstanceCommand : IRequest<QueryWorkflowInstanceCommandResponse>, IModelValidator<QueryWorkflowInstanceCommand>
{
    /// <summary>
    /// 工作流实例 ID（执行历史记录 ID）.
    /// </summary>
    public Guid Id { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWorkflowInstanceCommand> validate)
    {
        validate.RuleFor(x => x.Id)
            .NotEmpty().WithMessage("工作流实例 ID 不能为空");
    }
}
