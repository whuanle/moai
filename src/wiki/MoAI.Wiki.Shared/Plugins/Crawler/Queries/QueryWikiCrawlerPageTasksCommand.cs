using FluentValidation;
using MediatR;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.Crawler.Queries;

/// <summary>
/// 查询这个爬虫的所有任务状态.
/// </summary>
public class QueryWikiCrawlerPageTasksCommand : IRequest<QueryWikiCrawlerPageTasksCommandResponse>, IModelValidator<QueryWikiCrawlerPageTasksCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<QueryWikiCrawlerPageTasksCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}