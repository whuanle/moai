namespace MoAI.Wiki.Plugins.Feishu.Models;

/// <summary>
/// 爬取状态.
/// </summary>
public class QueryWikiFeishuPageTasksCommandResponse
{
    /// <summary>
    /// 爬取完成的地址.
    /// </summary>
    public IReadOnlyCollection<WikiFeishuPageItem> Items { get; init; } = Array.Empty<WikiFeishuPageItem>();
}
