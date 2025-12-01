using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.WikiCrawler.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// DeleteWikiCrawlerConfigCommandValidators.
/// </summary>
public class DeleteWikiCrawlerConfigCommandValidators : Validator<DeleteWikiCrawlerConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiCrawlerConfigCommandValidators"/> class.
    /// </summary>
    public DeleteWikiCrawlerConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
