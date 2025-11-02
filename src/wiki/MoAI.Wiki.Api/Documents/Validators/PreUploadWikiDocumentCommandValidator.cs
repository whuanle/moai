using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.DocumentManager.Handlers;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// PreUploadWikiDocumentCommandValidator.
/// </summary>
public class PreUploadWikiDocumentCommandValidator : Validator<PreUploadWikiDocumentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadWikiDocumentCommandValidator"/> class.
    /// </summary>
    public PreUploadWikiDocumentCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不正确.")
            .Length(2, 50).WithMessage("文件类型不正确.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件 MD5 不能为空.")
            .Length(32).WithMessage("文件 MD5 长度必须为 32 位.")
            .Matches("^[a-fA-F0-9]{32}$").WithMessage("文件 MD5 格式不正确.");

        RuleFor(x => x.FileSize)
            .Must(x => x < 1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
    }
}
