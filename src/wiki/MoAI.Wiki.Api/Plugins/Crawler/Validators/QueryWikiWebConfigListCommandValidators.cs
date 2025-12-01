using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.WikiCrawler.Queries;

namespace MoAI.Wiki.WikiCrawler.Validators;

/// <summary>
/// QueryWikiConfigInfoCommandValidators.
/// </summary>
public class QueryWikiCrawlerConfigListCommandValidators : Validator<QueryWikiCrawlerConfigListCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerConfigListCommandValidators"/> class.
    /// </summary>
    public QueryWikiCrawlerConfigListCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
