namespace MoAI.Gateway.Queries.Responses;

/// <summary>
/// 团队可用网关模型项.
/// </summary>
public class TeamGatewayModelItem
{
    /// <summary>
    /// ai模型 id.
    /// </summary>
    public Guid AiModelId { get; set; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 网关调用时使用的模型 id（即渠道模型 id）.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 所属渠道名称.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// 渠道供应商标识.
    /// </summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>
    /// 额度限制，null=不限额.
    /// </summary>
    public TeamGatewayModelQuota? Quota { get; set; }

    /// <summary>
    /// 该团队通过开放接口累计消耗 tokens.
    /// </summary>
    public long TotalUsedTokens { get; set; }
}
