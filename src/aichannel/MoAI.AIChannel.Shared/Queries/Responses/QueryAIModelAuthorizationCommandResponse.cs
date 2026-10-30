namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 模型授权查询响应.
/// </summary>
public class QueryAIModelAuthorizationCommandResponse
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 是否公开：true=所有团队可用；false=私有，仅授权团队可用.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 授权团队集合（私有模型）.
    /// </summary>
    public IReadOnlyList<QueryAIModelAuthorizationCommandResponseItem> Items { get; set; } = new List<QueryAIModelAuthorizationCommandResponseItem>();
}
