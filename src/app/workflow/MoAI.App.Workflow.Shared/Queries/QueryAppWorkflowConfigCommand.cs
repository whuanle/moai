using FluentValidation;
using MediatR;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Queries;

/// <summary>
/// 查询流程应用编排配置（草稿/已发布定义、编辑器画布 JSON、版本与状态），仅团队成员可访问；未保存过配置时返回空.
/// </summary>
public class QueryAppWorkflowConfigCommand : IRequest<QueryAppWorkflowConfigCommandResponse>, IUserIdContext, IModelValidator<QueryAppWorkflowConfigCommand>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppWorkflowConfigCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
