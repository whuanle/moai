namespace MoAI.Wiki.Wikis.Queries.Response;

/// <summary>
/// 查询知识库创建信息.
/// </summary>
public class QueryWikiCreatorCommandResponse
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 是否存在.
    /// </summary>
    public bool WikiIsExist { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 创建这 id.
    /// </summary>
    public int CreatorId { get; init; }
}