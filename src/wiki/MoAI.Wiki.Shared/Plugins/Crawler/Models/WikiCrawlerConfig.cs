using FluentValidation;
using MediatR;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// 爬虫配置.
/// </summary>
public class WikiCrawlerConfig : IWikiPluginKey, IModelValidator<WikiCrawlerConfig>
{
    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// 限制自动爬取的网页都在该路径之下,limit_address跟address必须具有相同域名.
    /// </summary>
    public string? LimitAddress { get; set; }

    /// <summary>
    /// 伪装标识.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>
    /// 超时时间，秒.
    /// </summary>
    public int TimeOutSecond { get; init; } = 10;

    /// <summary>
    /// 新抓取的页面最大数量，0不限制.
    /// </summary>
    public int LimitMaxNewCount { get; set; }

    /// <summary>
    /// 队列最大任务数量.
    /// </summary>
    public int LimitMaxCount { get; set; }

    /// <summary>
    /// 是否抓取其它页面.
    /// </summary>
    public bool IsCrawlOther { get; set; }

    /// <summary>
    /// 是否覆盖已存在的页面.
    /// </summary>
    public bool IsOverExistPage { get; init; }

    /// <summary>
    /// 选择器，筛选页面的表达式，例如 ".content".
    /// </summary>
    public string Selector { get; set; } = default!;

    /// <inheritdoc/>
    public string PluginKey => "crawler";

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<WikiCrawlerConfig> validate)
    {
        validate.RuleFor(x => x.Address)
            .NotEmpty().WithMessage("抓取地址不能为空")
            .Must(a => Uri.IsWellFormedUriString(a, UriKind.Absolute)).WithMessage("抓取地址格式不正确");
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
