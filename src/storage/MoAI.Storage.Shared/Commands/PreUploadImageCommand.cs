using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 图片预上传命令，生成预签名上传地址.
/// </summary>
public class PreUploadImageCommand : IRequest<PreUploadFileCommandResponse>, IModelValidator<PreUploadImageCommand>
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
    /// 文件大小 (字节).
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadImageCommand> validate)
    {
        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名不能为空");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不能为空");

        validate.RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于0");

        validate.RuleFor(x => x.SHA256)
            .NotEmpty().WithMessage("文件SHA256不能为空");
    }
}
