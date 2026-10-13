using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Timer.Commands;

/// <summary>
/// 删除团队定时器（软删除并移除调度），需要团队 Admin 及以上角色.
/// </summary>
public class DeleteTeamTimerCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteTeamTimerCommand>, IUserIdContext
{
    /// <summary>
    /// 定时器 id.
    /// </summary>
    public Guid TimerId { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteTeamTimerCommand> validate)
    {
        validate.RuleFor(x => x.TimerId).NotEqual(Guid.Empty).WithMessage("定时器 id 不正确.");
    }
}
