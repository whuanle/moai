namespace MoAI.LoginSafety.Models;

/// <summary>
/// 风控评估结果.
/// </summary>
public class RiskEvaluationResult
{
    /// <summary>
    /// 综合风险等级.
    /// </summary>
    public LoginRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// 命中的规则说明（人话描述列表）.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 本次登录是否触发该 IP 自动封禁（封禁后同 IP 后续登录将被拒绝）.
    /// </summary>
    public bool IpBanned { get; init; }
}
