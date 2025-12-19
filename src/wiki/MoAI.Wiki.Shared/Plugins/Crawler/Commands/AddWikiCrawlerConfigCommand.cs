using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.Crawler.Commands;

/// <summary>
/// 添加一个网页爬取配置.
/// </summary>
public class AddWikiCrawlerConfigCommand : WikiCrawlerConfig, IRequest<SimpleInt>, IModelValidator<AddWikiCrawlerConfigCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddWikiCrawlerConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        validate.RuleFor(x => x.LimitMaxCount)
            .GreaterThan(0).WithMessage("最大抓取数量必须大于0")
            .LessThanOrEqualTo(1000).WithMessage("最大抓取数量不能超过1000");

        validate.RuleFor(x => x.LimitAddress)
            .Must((a, b) =>
            {
                if (!string.IsNullOrEmpty(b))
                {
                    return new Uri(a.Address).Host == new Uri(b).Host;
                }

                return true;
            }).WithMessage("限制地址域名必须跟抓取地址一致");
    }
}