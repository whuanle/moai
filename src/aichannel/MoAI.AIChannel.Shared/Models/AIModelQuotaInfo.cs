using System;

namespace MoAI.AIChannel.Models;

/// <summary>
/// 模型额度信息，来源于额度规则与其余额行.
/// </summary>
public class AIModelQuotaInfo
{
    /// <summary>
    /// 额度规则 id.
    /// </summary>
    public int LimitId { get; set; }

    /// <summary>
    /// 重置周期单位：0=不重置(总量一次性) 1=小时 2=天 3=周 4=月.
    /// </summary>
    public int PeriodUnit { get; set; }

    /// <summary>
    /// 重置周期长度，与 period_unit 配合，例如每 8 小时=(8,1)、每天=(1,2)；period_unit=0 时无效.
    /// </summary>
    public int PeriodValue { get; set; }

    /// <summary>
    /// 每个重置周期内的 tokens 上限.
    /// </summary>
    public long LimitValue { get; set; }

    /// <summary>
    /// 当前周期已消耗 tokens.
    /// </summary>
    public long UsedTokens { get; set; }

    /// <summary>
    /// 规则本身的有效期，null=长期有效.
    /// </summary>
    public DateTime? ExpirationTime { get; set; }
}
