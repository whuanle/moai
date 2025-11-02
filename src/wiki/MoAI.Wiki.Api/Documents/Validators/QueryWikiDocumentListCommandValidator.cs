using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Documents.Queries;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// QueryWikiDocumentListCommandValidator.
/// </summary>
public class QueryWikiDocumentListCommandValidator : Validator<QueryWikiDocumentListCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentListCommandValidator"/> class.
    /// </summary>
    public QueryWikiDocumentListCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
