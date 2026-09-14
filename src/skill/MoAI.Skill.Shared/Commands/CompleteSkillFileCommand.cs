using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 完成技能包文件上传，仅平台管理员可调用.
/// </summary>
public class CompleteSkillFileCommand : IRequest<EmptyCommandResponse>, IModelValidator<CompleteSkillFileCommand>
{
    /// <summary>
    /// 文件 id.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CompleteSkillFileCommand> validate)
    {
        validate.RuleFor(x => x.FileId).GreaterThan(0).WithMessage("文件 id 不正确.");
    }
}
