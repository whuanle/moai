using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 启用/禁用技能，仅平台管理员可调用；禁用后应用运行时不再加载.
/// </summary>
public class SetSkillDisableCommand : IRequest<EmptyCommandResponse>, IModelValidator<SetSkillDisableCommand>
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetSkillDisableCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
