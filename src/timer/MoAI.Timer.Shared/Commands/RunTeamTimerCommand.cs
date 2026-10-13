using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Timer.Commands;

/// <summary>
/// 立即执行团队定时器（不入调度，异步执行一次），需要团队 Admin 及以上角色；返回执行记录 id.
/// </summary>
public class RunTeamTimerCommand : IRequest<SimpleGuid>, IModelValidator<RunTeamTimerCommand>, IUserIdContext
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
    public static void Validate(AbstractValidator<RunTeamTimerCommand> validate)
    {
        validate.RuleFor(x => x.TimerId).NotEqual(Guid.Empty).WithMessage("定时器 id 不正确.");
    }
}
