using FluentValidation;
using MediatR;
using MoAI.Storage.Models;

namespace MoAI.Skill.Commands;

/// <summary>
/// 预上传技能包文件，生成预签名上传地址，仅平台管理员可调用.
/// </summary>
public class PreUploadSkillFileCommand : IRequest<PreUploadSkillFileCommandResponse>, IModelValidator<PreUploadSkillFileCommand>
{
    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 文件类型 (MIME Type).
    /// </summary>
    public string ContentType { get; init; } = default!;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadSkillFileCommand> validate)
    {
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
        validate.RuleFor(x => x.ContentType).NotEmpty().WithMessage("文件类型不能为空.");
        validate.RuleFor(x => x.FileSize).GreaterThan(0).WithMessage("文件大小必须大于 0.").LessThan(10 * 1024 * 1024).WithMessage("技能包文件不能超过 10MB.");
        validate.RuleFor(x => x.SHA256).NotEmpty().WithMessage("文件 SHA256 不能为空.");
    }
}

/// <summary>
/// 预上传技能包文件响应.
/// </summary>
public class PreUploadSkillFileCommandResponse
{
    /// <summary>
    /// 文件是否已存在，如已存在则无需再次上传.
    /// </summary>
    public bool IsExist { get; init; }

    /// <summary>
    /// 文件 ID.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 预签名上传地址，当 IsExist = true 时为空.
    /// </summary>
    public Uri? UploadUrl { get; init; }

    /// <summary>
    /// 签名过期时间，当 IsExist = true 时为空.
    /// </summary>
    public DateTimeOffset? Expiration { get; init; }
}
