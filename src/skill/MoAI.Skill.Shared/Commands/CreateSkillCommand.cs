using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Skill.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 创建技能，仅平台管理员可调用.
/// </summary>
public class CreateSkillCommand : IRequest<SimpleGuid>, IModelValidator<CreateSkillCommand>
{
    /// <summary>
    /// 技能标识，全局唯一，蛇形命名，创建后不可变更.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 技能描述，作为 Agent 工具列表中的能力说明.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 使用说明（markdown），技能加载时注入给 Agent.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// 技能包文件清单.
    /// </summary>
    public IReadOnlyList<SkillFileItem> Files { get; init; } = Array.Empty<SkillFileItem>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateSkillCommand> validate)
    {
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("技能标识不能为空.")
            .Matches("^[a-z][a-z0-9_]{0,29}$").WithMessage("技能标识仅允许小写字母开头，包含小写字母/数字/下划线，最长 30.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("技能名称不能为空.").MaximumLength(50).WithMessage("技能名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("技能描述最长 255 个字符.");
        validate.RuleFor(x => x.Files).Must(files => files.Select(f => f.Path).Distinct().Count() == files.Count)
            .WithMessage("技能包内文件路径不能重复.");
        validate.RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(f => f.Path).NotEmpty().WithMessage("文件路径不能为空.")
                .Matches("^[a-zA-Z0-9_][a-zA-Z0-9_/.-]{0,199}$").WithMessage("文件路径仅允许字母/数字/下划线/点/横线/斜杠，最长 200.")
                .Must(p => !p.Contains("..") && !p.StartsWith('/')).WithMessage("文件路径不允许包含 .. 或以 / 开头.");
            file.RuleFor(f => f.FileId).GreaterThan(0).WithMessage("文件 id 不正确.");
        });
    }
}
