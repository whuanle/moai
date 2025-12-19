using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.Feishu.Commands;

/// <summary>
/// 修改一个网页爬取配置.
/// </summary>
public class UpdateWikiCrawlerConfigCommand : WikiCrawlerConfig, IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiCrawlerConfigCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiCrawlerConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        validate.RuleFor(x => x.LimitMaxCount)
            .GreaterThan(0).WithMessage("最大抓取数量必须大于0")
            .LessThanOrEqualTo(1000).WithMessage("最大抓取数量不能超过1000");

        validate.RuleFor(x => x.Selector)
            .MaximumLength(255).WithMessage("页面选择器规则不能超过255字符");
    }
}
