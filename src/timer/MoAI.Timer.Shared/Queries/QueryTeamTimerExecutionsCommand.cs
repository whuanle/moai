using FluentValidation;
using MediatR;
using MoAI.Timer.Queries.Responses;

namespace MoAI.Timer.Queries;

/// <summary>
/// 查询团队定时器执行历史（按触发时间倒序），仅团队成员可访问.
/// </summary>
public class QueryTeamTimerExecutionsCommand : IRequest<QueryTeamTimerExecutionsCommandResponse>, IModelValidator<QueryTeamTimerExecutionsCommand>
{
    /// <summary>
    /// 定时器 id.
    /// </summary>
    public Guid TimerId { get; init; }

    /// <summary>
    /// 页码，从 1 开始.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 页大小，默认加载最近 10 条.
    /// </summary>
    public int PageSize { get; init; } = 10;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamTimerExecutionsCommand> validate)
    {
        validate.RuleFor(x => x.TimerId).NotEqual(Guid.Empty).WithMessage("定时器 id 不正确.");
        validate.RuleFor(x => x.PageNo).GreaterThan(0).WithMessage("页码从 1 开始.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("页大小仅允许 1-100.");
    }
}
