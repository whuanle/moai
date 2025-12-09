using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Plugins.Crawler.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Validators;

/// <summary>
/// AddWikiCrawlerConfigCommandValidators.
/// </summary>
public class AddWikiCrawlerConfigCommandValidators : AbstractValidator<AddWikiCrawlerConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiCrawlerConfigCommandValidators"/> class.
    /// </summary>
    public AddWikiCrawlerConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        RuleFor(x => x.Config).NotNull();

        RuleFor(x => x.Config.LimitMaxCount)
            .GreaterThan(0).WithMessage("最大抓取数量必须大于0")
            .LessThanOrEqualTo(1000).WithMessage("最大抓取数量不能超过1000");

        RuleFor(x => x.Config.LimitAddress)
            .Must((a, b) =>
            {
                if (!string.IsNullOrEmpty(b))
                {
                    return new Uri(a.Config.Address).Host == new Uri(b).Host;
                }

                return true;
            }).WithMessage("限制地址域名必须跟抓取地址一致");
    }
}
