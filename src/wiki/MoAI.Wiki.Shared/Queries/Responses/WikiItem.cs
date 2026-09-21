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
    /// 文档数量.
    /// </summary>
    public int DocumentCount { get; set; }

    /// <summary>
    /// 已切片数量（切片内容表行数）.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// 最近一次文档更新时间，无文档时为 null.
    /// </summary>
    public DateTimeOffset? LastDocumentUpdateTime { get; set; }

    /// <summary>
    /// 知识库头像的 ObjectKey（空串=未设置）.
    /// </summary>
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
