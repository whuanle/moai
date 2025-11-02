using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Documents.Queries;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// SearchWikiDocumentTextCommandValidator.
/// </summary>
public class SearchWikiDocumentTextCommandValidator : Validator<SearchWikiDocumentTextCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchWikiDocumentTextCommandValidator"/> class.
    /// </summary>
    public SearchWikiDocumentTextCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}