using FluentValidation;
using MediatR;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Queries;

/// <summary>
/// 查询工作流定义命令.
/// 用于检索单个工作流定义的完整信息，包括所有节点配置和连接.
/// 基础信息（Name、Description、Avatar）来自 AppEntity.
/// 设计数据（UiDesign、FunctionDesign）来自 AppWorkflowDesignEntity.
/// </summary>
public class QueryWorkflowDefinitionCommand : IRequest<QueryWorkflowDefinitionCommandResponse>, IModelValidator<QueryWorkflowDefinitionCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用 ID（AppEntity.Id）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWorkflowDefinitionCommand> validate)
    {
        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用 ID 不能为空");
    }
}
