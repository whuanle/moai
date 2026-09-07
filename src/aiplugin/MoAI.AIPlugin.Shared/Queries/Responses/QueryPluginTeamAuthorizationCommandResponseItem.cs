namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 私有系统插件团队授权查询响应项.
/// </summary>
public class QueryPluginTeamAuthorizationCommandResponseItem
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string TeamName { get; init; } = string.Empty;
}
