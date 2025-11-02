namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryWikiDetailInfoResponse
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public Guid WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 团队头像路径.
    /// </summary>
    public required string AvatarUrl { get; set; }

    /// <summary>
    /// 知识库描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 公开使用，所有人不需要加入团队即可使用此知识库.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 知识库详细介绍.
    /// </summary>
    public string Markdown { get; set; } = default!;
}