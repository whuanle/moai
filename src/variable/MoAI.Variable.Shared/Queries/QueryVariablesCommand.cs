using FluentValidation;
using MediatR;
using MoAI.Variable.Queries.Responses;

namespace MoAI.Variable.Queries;

/// <summary>
/// 查询团队变量列表，仅团队成员可访问；私密变量值对成员掩码（仅管理员可见）.
/// </summary>
public class QueryVariablesCommand : IRequest<QueryVariablesCommandResponse>, IModelValidator<QueryVariablesCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 按变量名称精确筛选，空为不过滤.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 按变量名/描述模糊筛选，空为不过滤.
    /// </summary>
    public string? Keyword { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryVariablesCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).MaximumLength(50).WithMessage("变量名称最长 50 个字符.");
        validate.RuleFor(x => x.Keyword).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
    }
}
