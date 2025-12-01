using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.DocumentManager.Handlers;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// ComplateUploadWikiDocumentCommandValidator.
/// </summary>
public class ComplateUploadWikiDocumentCommandValidator : Validator<ComplateUploadWikiDocumentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateUploadWikiDocumentCommandValidator"/> class.
    /// </summary>
    public ComplateUploadWikiDocumentCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");
    }
}
