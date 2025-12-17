using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// Plugin 配置.
/// </summary>
public class WikiCrawlerPluginConfigSimpleItem : AuditsInfo
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
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 运行信息.
    /// </summary>
    public string WorkMessage { get; set; } = default!;

    /// <summary>
    /// 状态.
    /// </summary>
    public WorkerState WorkState { get; set; }

    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// 页面数量.
    /// </summary>
    public int PageCount { get; set; }
}