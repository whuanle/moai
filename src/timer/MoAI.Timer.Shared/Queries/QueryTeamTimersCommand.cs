using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Timer.Queries.Responses;

namespace MoAI.Timer.Queries;

/// <summary>
/// 查询团队定时器列表，仅团队成员可访问.
/// </summary>
public class QueryTeamTimersCommand : IRequest<QueryTeamTimersCommandResponse>, IModelValidator<QueryTeamTimersCommand>, IUserIdContext
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 页码，从 1 开始.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 页大小.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 按名称/描述模糊筛选，空为不过滤.
    /// </summary>
    public string? Keyword { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamTimersCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.PageNo).GreaterThan(0).WithMessage("页码从 1 开始.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("页大小仅允许 1-100.");
        validate.RuleFor(x => x.Keyword).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
    }
}
