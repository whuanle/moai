using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Skill.Commands;

/// <summary>
/// 删除技能（软删除）：个人技能归属人、团队技能团队管理员或平台管理员可调用；系统内置技能不可删除.
/// </summary>
public class DeleteSkillCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteSkillCommand>, IUserIdContext
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteSkillCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
