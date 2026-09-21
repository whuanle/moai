using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// ai模型额度余额，按规则维度记录当前周期已消耗与剩余，剩余=total_limit-used_tokens.
/// </summary>
public partial class AiModelQuotumEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 逻辑关联ai_model_limit.id，每条启用中的规则对应一行余额.
    /// </summary>
    public int LimitId { get; set; }

    /// <summary>
    /// 模型id，冗余自规则，便于直接按模型查询.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 额度主体团队id，冗余自规则，0=模型全局/个人额度.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 当前周期总额度，规则变更时同步快照到本列.
    /// </summary>
    public long TotalLimit { get; set; }

    /// <summary>
    /// 当前周期已消耗tokens.
    /// </summary>
    public long UsedTokens { get; set; }

    /// <summary>
    /// 当前重置周期起点.
    /// </summary>
    public DateTimeOffset PeriodStart { get; set; }

    /// <summary>
    /// 当前重置周期终点，定时任务扫描该列&lt;=now的行执行重置.
    /// </summary>
    public DateTimeOffset PeriodEnd { get; set; }

    /// <summary>
    /// 最近一次重置时间，null=创建后从未重置.
    /// </summary>
    public DateTimeOffset LastResetTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
