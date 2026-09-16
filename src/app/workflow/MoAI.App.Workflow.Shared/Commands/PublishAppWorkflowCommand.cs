using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Commands;

/// <summary>
/// 发布流程应用编排：校验草稿定义合法后生成不可变的已发布快照（版本号递增），并置应用为已发布.
/// 需要团队 Admin 及以上角色.
/// </summary>
public class PublishAppWorkflowCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<PublishAppWorkflowCommand>
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
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PublishAppWorkflowCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
