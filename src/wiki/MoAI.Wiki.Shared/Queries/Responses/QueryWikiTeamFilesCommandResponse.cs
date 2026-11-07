namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 跨知识库文件名搜索结果.
/// </summary>
public class QueryWikiTeamFilesCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 命中总数（排序截断前的全量匹配数）.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 命中的文件列表（按相关度排序：精确匹配 &gt; 前缀匹配 &gt; 包含，同档按更新时间倒序）.
    /// </summary>
    public IList<WikiTeamFileItem> Items { get; set; } = [];
}

/// <summary>
/// 跨知识库命中的文件项.
/// </summary>
public class WikiTeamFileItem
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// 所属知识库 id.
    /// </summary>
    public long WikiId { get; set; }

    /// <summary>
    /// 所属知识库名称.
    /// </summary>
    public string WikiName { get; set; } = default!;

    /// <summary>
    /// 文件名.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 最近更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
