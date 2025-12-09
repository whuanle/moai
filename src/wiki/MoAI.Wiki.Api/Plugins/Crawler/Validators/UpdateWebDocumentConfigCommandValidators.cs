using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.Feishu.Commands;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// UpdateWikiCrawlerConfigCommandValidators.
/// </summary>
public class UpdateWikiCrawlerConfigCommandValidators : AbstractValidator<UpdateWikiCrawlerConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiCrawlerConfigCommandValidators"/> class.
    /// </summary>
    public UpdateWikiCrawlerConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        RuleFor(x => x.Config.LimitMaxCount)
            .GreaterThan(0).WithMessage("最大抓取数量必须大于0")
            .LessThanOrEqualTo(1000).WithMessage("最大抓取数量不能超过1000");

        RuleFor(x => x.Config.Selector)
            .MaximumLength(255).WithMessage("页面选择器规则不能超过255字符");
    }
}
