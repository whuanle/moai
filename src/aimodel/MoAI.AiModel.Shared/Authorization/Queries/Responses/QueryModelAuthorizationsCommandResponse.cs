namespace MoAI.AiModel.Authorization.Queries.Responses;

/// <summary>
/// 模型授权列表响应.
/// </summary>
public class QueryModelAuthorizationsCommandResponse
{
    /// <summary>
    /// 模型授权列表.
    /// </summary>
    public IReadOnlyCollection<ModelAuthorizationItem> Models { get; init; } = new List<ModelAuthorizationItem>();
}
