using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Timer.Commands;

/// <summary>
/// 创建团队定时器，需要团队 Admin 及以上角色；绑定应用必须是本团队已发布的 Agent/流程应用.
/// </summary>
public class CreateTeamTimerCommand : IRequest<SimpleGuid>, IModelValidator<CreateTeamTimerCommand>, IUserIdContext
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 定时器名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 绑定的应用 id（本团队已发布的 Agent/流程应用）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 调度 cron 表达式（UTC，5 段标准格式）.
    /// </summary>
    public string CronExpression { get; init; } = default!;

    /// <summary>
    /// 每次执行时作为提问发送给应用的内容.
    /// </summary>
    public string InputContent { get; init; } = default!;

    /// <summary>
    /// 是否启用调度.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 失败重试次数（0-5，不含首次执行）.
    /// </summary>
    public int MaxRetryCount { get; init; }

    /// <summary>
    /// 重试间隔秒数（10-3600）.
    /// </summary>
    public int RetryIntervalSeconds { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateTeamTimerCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("定时器名称不能为空.").MaximumLength(100).WithMessage("定时器名称最长 100 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.AppId).NotEqual(Guid.Empty).WithMessage("请选择要绑定的应用.");
        validate.RuleFor(x => x.CronExpression).NotEmpty().WithMessage("调度 cron 表达式不能为空.").MaximumLength(100).WithMessage("cron 表达式最长 100 个字符.");
        validate.RuleFor(x => x.InputContent).NotEmpty().WithMessage("执行内容不能为空.").MaximumLength(8000).WithMessage("执行内容最长 8000 个字符.");
        validate.RuleFor(x => x.MaxRetryCount).InclusiveBetween(0, 5).WithMessage("失败重试次数仅允许 0-5.");
        validate.RuleFor(x => x.RetryIntervalSeconds).InclusiveBetween(10, 3600).WithMessage("重试间隔仅允许 10-3600 秒.");
    }
}
