using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Queries;

/// <summary>
/// 查询流程应用运行实例详情（含每个节点的执行状态），需要团队管理员.
/// </summary>
public class QueryAppWorkflowInstanceCommand : IRequest<QueryAppWorkflowInstanceCommandResponse>, IUserIdContext, IModelValidator<QueryAppWorkflowInstanceCommand>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 实例 id.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppWorkflowInstanceCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
