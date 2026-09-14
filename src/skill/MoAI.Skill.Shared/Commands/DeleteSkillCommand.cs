using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 删除技能（软删除），仅平台管理员可调用；系统内置技能不可删除.
/// </summary>
public class DeleteSkillCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteSkillCommand>
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteSkillCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
