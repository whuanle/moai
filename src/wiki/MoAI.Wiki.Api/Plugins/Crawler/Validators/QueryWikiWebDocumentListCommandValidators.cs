using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.WikiCrawler.Queries;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// QueryWikiConfigInfoCommandValidators.
/// </summary>
public class QueryWikiCrawlerListCommandValidators : Validator<QueryWikiCrawlerListCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerListCommandValidators"/> class.
    /// </summary>
    public QueryWikiCrawlerListCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.WikiCrawlerConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
