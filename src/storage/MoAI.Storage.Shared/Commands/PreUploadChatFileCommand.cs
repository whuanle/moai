using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Helpers;
using MoAI.Storage.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 对话附件预上传命令，生成预签名上传地址（文档/图片，公开目录，供发送前文本提取）.
/// </summary>
public class PreUploadChatFileCommand : IRequest<PreUploadFileCommandResponse>, IModelValidator<PreUploadChatFileCommand>
{
    /// <summary>
    /// 附件大小上限（20MB）.
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
    public static void Validate(AbstractValidator<PreUploadChatFileCommand> validate)
    {
        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名不能为空");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不能为空");

        validate.RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于0")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("附件大小不能超过20MB");

        validate.RuleFor(x => x.SHA256)
            .NotEmpty().WithMessage("文件SHA256不能为空");

        validate.RuleFor(x => x.FileName)
            .Must(name => FileStoreHelper.DocumentFormats.Contains(Path.GetExtension(name).ToLowerInvariant())
                || FileStoreHelper.ImageExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage("仅支持文档与图片格式的附件");
    }
}
