using FluentValidation;
using MediatR;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 分页查询技能列表，仅平台管理员可调用.
/// </summary>
public class QuerySkillsCommand : IRequest<QuerySkillsCommandResponse>, IModelValidator<QuerySkillsCommand>
{
    /// <summary>
    /// 页码，从 1 开始.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量，1-100.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 按技能标识/名称/描述模糊筛选，空为不过滤.
    /// </summary>
    public string? SearchText { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySkillsCommand> validate)
    {
        validate.RuleFor(x => x.PageNo).GreaterThanOrEqualTo(1).WithMessage("页码从 1 开始.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页数量 1-100.");
        validate.RuleFor(x => x.SearchText).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
    }
}
