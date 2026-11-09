using FluentValidation;
using MediatR;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 知识图谱文件导入预上传命令，生成预签名上传地址（文档/文本提取能力全集，公开目录，供 AI 导入提取）.
/// </summary>
public class PreUploadGraphFileCommand : IRequest<PreUploadFileCommandResponse>, IModelValidator<PreUploadGraphFileCommand>
{
    /// <summary>
    /// 文件大小上限（20MB）.
    /// </summary>
    public const int MaxFileSize = 20 * 1024 * 1024;

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 文件类型 (MIME Type).
    /// </summary>
    public string ContentType { get; init; } = default!;

    /// <summary>
    /// 文件大小 (字节).
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadGraphFileCommand> validate)
    {
        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名不能为空");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不能为空");

        validate.RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于0")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("文件大小不能超过20MB");

        validate.RuleFor(x => x.SHA256)
            .NotEmpty().WithMessage("文件SHA256不能为空");

        validate.RuleFor(x => x.FileName)
            .Must(name => DocumentFormatConst.ExtractableFormats.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage($"仅支持文档与文本格式文件（{DocumentFormatConst.ExtractableFormatsJoined}）.");
    }
}
