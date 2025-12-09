using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Documents.Queries;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// QueryWikiDocumentInfoCommandValidator.
/// </summary>
public class QueryWikiDocumentTaskListCommandValidator : AbstractValidator<QueryWikiDocumentTaskListCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentTaskListCommandValidator"/> class.
    /// </summary>
    public QueryWikiDocumentTaskListCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}
