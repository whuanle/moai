namespace MoAI.Gateway.Queries.Responses;

/// <summary>
/// 团队可用网关模型列表响应.
/// </summary>
public class QueryTeamGatewayModelsCommandResponse
{
    /// <summary>
    /// 模型集合.
    /// </summary>
    public IReadOnlyList<TeamGatewayModelItem> Items { get; set; } = new List<TeamGatewayModelItem>();
}
