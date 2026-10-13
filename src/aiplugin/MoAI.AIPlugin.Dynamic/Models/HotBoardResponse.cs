using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 新闻热榜响应结果.
/// </summary>
public class HotBoardResponse
{
    /// <summary>
    /// 新闻热榜数据列表.
    /// </summary>
    [Description("新闻热榜数据列表")]
    public IReadOnlyList<HotBoardNewsItem> NewsList { get; init; } = [];

    /// <summary>
    /// 抓取失败的来源及原因.
    /// </summary>
    [Description("抓取失败的来源及原因（空列表表示全部成功）")]
    public IReadOnlyList<string> Errors { get; init; } = [];
}
