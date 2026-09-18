using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 更新技能：个人技能归属人、团队技能团队管理员或平台管理员可调用；技能标识不可修改.
/// </summary>
public class UpdateSkillCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateSkillCommand>, IUserIdContext
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

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 技能描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 使用说明（markdown），技能加载时注入给 Agent.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// 技能包文件清单，整体替换.
    /// </summary>
    public IReadOnlyList<SkillFileItem> Files { get; init; } = Array.Empty<SkillFileItem>();

    /// <summary>
    /// 分类 id，0 表示未分类，分类类型必须为 skill.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateSkillCommand> validate)
    {
        // SkillId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("技能名称不能为空.").MaximumLength(50).WithMessage("技能名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("技能描述最长 255 个字符.");
        validate.RuleFor(x => x.ClassifyId).GreaterThanOrEqualTo(0).WithMessage("分类 id 不正确.");
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
