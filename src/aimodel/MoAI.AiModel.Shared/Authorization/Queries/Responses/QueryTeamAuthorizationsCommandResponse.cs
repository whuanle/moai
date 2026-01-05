namespace MoAI.AiModel.Authorization.Queries.Responses;

/// <summary>
/// 团队授权列表响应.
/// </summary>
public class QueryTeamAuthorizationsCommandResponse
{
    /// <summary>
    /// 团队授权列表.
    /// </summary>
    public IReadOnlyCollection<TeamAuthorizationItem> Teams { get; init; } = new List<TeamAuthorizationItem>();
}
