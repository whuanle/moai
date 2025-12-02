using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.Crawler.Commands;
using MoAI.Wiki.Plugins.Feishu.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// StartWikiCrawlerCommandValidators.
/// </summary>
public class StartWikiCrawlerCommandValidators : Validator<UpdateWikiCrawlerConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiCrawlerCommandValidators"/> class.
    /// </summary>
    public StartWikiCrawlerCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}
