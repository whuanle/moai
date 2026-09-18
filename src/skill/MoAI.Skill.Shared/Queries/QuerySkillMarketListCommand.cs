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

    /// <summary>
    /// 按分类 id 过滤，为空查全部分类.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySkillMarketListCommand> validate)
    {
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
        validate.RuleFor(x => x.ClassifyId).GreaterThan(0).When(x => x.ClassifyId != null).WithMessage("分类 id 不正确.");
    }
}
