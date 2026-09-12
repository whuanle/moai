using FluentValidation;
using MediatR;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询技能详情，仅平台管理员可调用.
/// </summary>
public class QuerySkillCommand : IRequest<QuerySkillCommandResponse>, IModelValidator<QuerySkillCommand>
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySkillCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
