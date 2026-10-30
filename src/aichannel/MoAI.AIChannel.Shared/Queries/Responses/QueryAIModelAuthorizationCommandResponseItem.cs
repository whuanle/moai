namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 授权团队信息.
/// </summary>
public class QueryAIModelAuthorizationCommandResponseItem
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string TeamName { get; set; } = default!;
}
