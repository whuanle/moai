using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Documents.Commands;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// ClearWikiDocumentEmbeddingCommandValidator.
/// </summary>
public class ClearWikiDocumentEmbeddingCommandValidator : Validator<ClearWikiDocumentEmbeddingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClearWikiDocumentEmbeddingCommandValidator"/> class.
    /// </summary>
    public ClearWikiDocumentEmbeddingCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
