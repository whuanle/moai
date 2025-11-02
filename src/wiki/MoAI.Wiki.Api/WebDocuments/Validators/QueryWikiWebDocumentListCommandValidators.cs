using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.WebDocuments.Queries;

namespace MoAI.Wiki.WebDocuments.Validators;

/// <summary>
/// QueryWikiConfigInfoCommandValidators.
/// </summary>
public class QueryWikiWebDocumentListCommandValidators : Validator<QueryWikiWebDocumentListCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiWebDocumentListCommandValidators"/> class.
    /// </summary>
    public QueryWikiWebDocumentListCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.WikiWebConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
