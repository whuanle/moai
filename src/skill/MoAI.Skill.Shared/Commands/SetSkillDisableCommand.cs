using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Skill.Commands;

/// <summary>
/// 启用/禁用技能：个人技能归属人、团队技能团队管理员或平台管理员可调用；禁用后应用运行时不再加载.
/// </summary>
public class SetSkillDisableCommand : IRequest<EmptyCommandResponse>, IModelValidator<SetSkillDisableCommand>, IUserIdContext
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
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetSkillDisableCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
