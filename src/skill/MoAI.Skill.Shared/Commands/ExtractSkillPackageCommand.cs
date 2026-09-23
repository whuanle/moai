using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Skill.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 解压技能压缩包：读取已上传的 zip 文件，逐条目登记为技能包资源文件，并解析 SKILL.md 技能信息（frontmatter 名称/描述 + 正文使用说明）.
/// </summary>
public class ExtractSkillPackageCommand : IRequest<ExtractSkillPackageCommandResponse>, IModelValidator<ExtractSkillPackageCommand>
{
    /// <summary>
    /// 压缩包文件 id（file 表，须已上传完成）.
    /// </summary>
    public long FileId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExtractSkillPackageCommand> validate)
    {
        validate.RuleFor(x => x.FileId).GreaterThan(0).WithMessage("文件 id 不正确.");
    }
}

/// <summary>
/// 解压技能压缩包响应.
/// </summary>
public class ExtractSkillPackageCommandResponse
{
    /// <summary>
    /// 技能名称（解析自 SKILL.md frontmatter，无则为空串）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 技能描述（解析自 SKILL.md frontmatter，无则为空串）.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 使用说明（SKILL.md frontmatter 之后的正文，无则为空串）.
    /// </summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// 解压登记后的技能包文件清单.
    /// </summary>
    public IReadOnlyList<SkillFileItem> Files { get; init; } = Array.Empty<SkillFileItem>();
}
