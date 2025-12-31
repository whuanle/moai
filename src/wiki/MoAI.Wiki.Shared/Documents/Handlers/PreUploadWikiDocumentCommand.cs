using FluentValidation;
using MediatR;
using MoAI.Wiki.Documents.Models;

namespace MoAI.Wiki.Documents.Handlers;

/// <summary>
/// 预上传知识库文件.
/// </summary>
public class PreUploadWikiDocumentCommand : IRequest<PreUploadWikiDocumentCommandResponse>, IModelValidator<PreUploadWikiDocumentCommand>
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; } = default!;

    /// <summary>
    /// 文件 MD5.
    /// </summary>
    public string MD5 { get; set; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadWikiDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不正确.")
            .Length(2, 50).WithMessage("文件类型不正确.");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        validate.RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件 MD5 不能为空.")
            .Length(32).WithMessage("文件 MD5 长度必须为 32 位.")
            .Matches("^[a-fA-F0-9]{32}$").WithMessage("文件 MD5 格式不正确.");

        validate.RuleFor(x => x.FileSize)
            .Must(x => x < 1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
    }
}
