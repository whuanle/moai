using FluentValidation;
using MediatR;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询技能市场列表（已上架公开技能），所有登录用户可访问.
/// </summary>
public class QuerySkillMarketListCommand : IRequest<QuerySkillListCommandResponse>, IModelValidator<QuerySkillMarketListCommand>
{
    /// <summary>
    /// 按名称/标识/描述关键字筛选，空为不过滤.
    /// </summary>
    public string? Keywords { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySkillMarketListCommand> validate)
    {
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
    }
}
