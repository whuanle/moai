using MoAI.AIChannel.Models;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// 授权团队及其额度信息.
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

    /// <summary>
    /// 该团队的额度，未设置时为 null（不限额）.
    /// </summary>
    public AIModelQuotaInfo? Quota { get; set; }
}
