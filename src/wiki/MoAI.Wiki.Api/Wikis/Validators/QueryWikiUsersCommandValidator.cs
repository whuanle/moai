using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Validators;

/// <summary>
/// QueryWikiUsersCommandValidator
/// </summary>
public class QueryWikiUsersCommandValidator : Validator<QueryWikiUsersCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiUsersCommandValidator"/> class.
    /// </summary>
    public QueryWikiUsersCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
