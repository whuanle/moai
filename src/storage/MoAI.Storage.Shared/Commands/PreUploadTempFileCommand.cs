using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Commands.Response;

namespace MoAI.Storage.Commands;

/// <summary>
/// 临时文件预上传命令，生成预签名上传地址.
/// </summary>
public class PreUploadTempFileCommand : IRequest<PreUploadTempFileCommandResponse>, IModelValidator<PreUploadTempFileCommand>
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
    /// 文件 MD5.
    /// </summary>
    public string MD5 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadTempFileCommand> validate)
    {
        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名不能为空");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不能为空");

        validate.RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于0");

        validate.RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件MD5不能为空");
    }
}
