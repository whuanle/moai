using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.WebDocuments.Queries;

namespace MoAI.Wiki.WebDocuments.Validators;

/// <summary>
/// QueryWikiConfigInfoCommandValidators.
/// </summary>
public class QueryWikiWebConfigListCommandValidators : Validator<QueryWikiWebConfigListCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiWebConfigListCommandValidators"/> class.
    /// </summary>
    public QueryWikiWebConfigListCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
