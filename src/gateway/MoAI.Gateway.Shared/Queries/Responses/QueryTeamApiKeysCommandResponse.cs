namespace MoAI.Gateway.Queries.Responses;

/// <summary>
/// 团队网关 API Key 列表响应.
/// </summary>
public class QueryTeamApiKeysCommandResponse
{
    /// <summary>
    /// 密钥集合.
    /// </summary>
    public IReadOnlyList<TeamApiKeyItem> Items { get; set; } = new List<TeamApiKeyItem>();
}
