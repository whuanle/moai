namespace MoAI.Gateway.Queries.Responses;

/// <summary>
/// 模型额度状态.
/// </summary>
public class TeamGatewayModelQuota
{
    /// <summary>
    /// 周期长度，与 period_unit 配合.
    /// </summary>
    public int PeriodValue { get; set; }

    /// <summary>
    /// 重置周期单位：0=不重置(总量一次性) 1=小时 2=天 3=周 4=月.
    /// </summary>
    public int PeriodUnit { get; set; }

    /// <summary>
    /// 每周期 tokens 上限.
    /// </summary>
    public long LimitValue { get; set; }

    /// <summary>
    /// 当前周期已消耗 tokens.
    /// </summary>
    public long UsedTokens { get; set; }

    /// <summary>
    /// 当前周期终点，period_unit=0 时为 null.
    /// </summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>
    /// 规则有效期，null=长期有效.
    /// </summary>
    public DateTimeOffset? ExpirationTime { get; set; }
}
