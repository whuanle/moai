using FluentValidation;
using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询工作流定义命令.
/// 用于检索单个工作流定义的完整信息，包括所有节点配置和连接.
/// </summary>
public class QueryWorkflowDefinitionCommand : IRequest<QueryWorkflowDefinitionCommandResponse>, IModelValidator<QueryWorkflowDefinitionCommand>
{
    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 是否包含草稿数据.
    /// 如果为 true，返回草稿版本；否则返回已发布版本.
    /// </summary>
    public bool IncludeDraft { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWorkflowDefinitionCommand> validate)
    {
        validate.RuleFor(x => x.Id)
            .NotEmpty().WithMessage("工作流定义 ID 不能为空");
    }
}
