using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.Crawler.Queries;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// QueryWikiCrawlerConfigCommandValidators.
/// </summary>
public class QueryWikiCrawlerConfigCommandValidators : Validator<QueryWikiCrawlerConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerConfigCommandValidators"/> class.
    /// </summary>
    public QueryWikiCrawlerConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
