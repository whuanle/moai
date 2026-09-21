namespace MoAI.Wiki.Models;

/// <summary>
/// 携带网页爬虫配置的命令，用于复用创建/更新外部源的爬虫校验规则.
/// </summary>
public interface IWikiCrawlerSourceCommand
{
    /// <summary>
    /// 外部源类型.
    /// </summary>
    WikiSourceType SourceType { get; }

    /// <summary>
    /// 爬虫源配置，类型为爬虫时必填.
    /// </summary>
    WikiSourceCrawlerConfig? Crawler { get; }
}
