namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库项.
/// </summary>
public class WikiItem
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库简介.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
