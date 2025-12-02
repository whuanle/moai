namespace MoAI.Wiki.Plugins.Feishu.Models;

public class QueryWikiFeishuPageTasksCommandResponse
{
    /// <summary>
    /// 爬虫状态.
    /// </summary>
    public WikiFeishuTask Task { get; init; } = default!;

    /// <summary>
    /// 每一个地址.
    /// </summary>
    public IReadOnlyCollection<WikiFeishuPageItem> Pages { get; init; } = Array.Empty<WikiFeishuPageItem>();
}
