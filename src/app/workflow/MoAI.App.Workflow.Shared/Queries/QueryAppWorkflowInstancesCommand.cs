using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Queries;

/// <summary>
/// 分页查询流程应用的运行实例列表，需要团队管理员（查看日志同权限）.
/// </summary>
public class QueryAppWorkflowInstancesCommand : PagedParamter, IRequest<QueryAppWorkflowInstancesCommandResponse>, IUserIdContext, IModelValidator<QueryAppWorkflowInstancesCommand>
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
    /// 按实例状态过滤：0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消；为空不过滤.
    /// </summary>
    public short? Status { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppWorkflowInstancesCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Status).InclusiveBetween((short)0, (short)4).When(x => x.Status != null).WithMessage("实例状态不正确.");
    }
}
