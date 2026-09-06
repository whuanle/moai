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
/// ai模型额度规则，一行=某主体在一个重置周期内的tokens上限，只能用于系统模型.
/// </summary>
public partial class AiModelLimitEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 模型id.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 额度主体团队id，0=不限定团队.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 重置周期长度，与period_unit配合：每8小时=(8,1)、每天=(1,2)、每30天=(30,2)；period_unit=0时本列无效.
    /// </summary>
    public int PeriodValue { get; set; }

    /// <summary>
    /// 重置周期单位：0=不重置(总量一次性) 1=小时 2=天 3=周 4=月.
    /// </summary>
    public int PeriodUnit { get; set; }

    /// <summary>
    /// 每个重置周期内的tokens上限.
    /// </summary>
    public long LimitValue { get; set; }

    /// <summary>
    /// 规则本身的有效期，null=长期有效；注意区别于重置周期，到期后规则整体失效.
    /// </summary>
    public DateTime? ExpirationTime { get; set; }

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
