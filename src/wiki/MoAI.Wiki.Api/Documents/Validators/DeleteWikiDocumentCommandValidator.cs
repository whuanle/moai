using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.DocumentManager.Handlers;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// DeleteWikiDocumentCommandValidator.
/// </summary>
public class DeleteWikiDocumentCommandValidator : AbstractValidator<DeleteWikiDocumentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentCommandValidator"/> class.
    /// </summary>
    public DeleteWikiDocumentCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}
