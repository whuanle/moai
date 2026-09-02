namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库详情响应.
/// </summary>
public class QueryWikiCommandResponse
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
    /// 我在所属团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
