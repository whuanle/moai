using System.Collections.Generic;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 爬虫一轮抓取的汇总统计.
/// </summary>
public class CrawlerSyncResult
{
    /// <summary>
    /// 实际抓取处理的页面数（不含入队后未抓到者）.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 新建文档数.
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// 更新文档数.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// 内容无变化数.
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// 跳过数.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// 失败数.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// 触发工作流的文档数.
    /// </summary>
    public int WorkflowTriggered { get; set; }

    /// <summary>
    /// 是否因连续失败触发熔断而提前中止.
    /// </summary>
    public bool BreakerTripped { get; set; }

    /// <summary>
    /// 是否因达到页数/深度上限而截断（仍有待抓页面）.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// 逐页结果.
    /// </summary>
    public IList<CrawlerPageResult> Items { get; init; } = new List<CrawlerPageResult>();

    /// <summary>
    /// 生成结果摘要.
    /// </summary>
    /// <returns>摘要文本.</returns>
    public string BuildSummary()
    {
        var summary = $"共抓取 {Total} 个页面：新建 {Created}，更新 {Updated}，无变化 {Unchanged}，跳过 {Skipped}，失败 {Failed}"
            + (WorkflowTriggered > 0 ? $"，触发工作流 {WorkflowTriggered}" : string.Empty);

        if (BreakerTripped)
        {
            summary += $"；连续失败达 {WikiSourceDefaults.CrawlerMaxConsecutiveFailures} 次已提前中止";
        }
        else if (Truncated)
        {
            summary += "；已达抓取上限，剩余页面留待下轮";
        }

        return summary;
    }
}
