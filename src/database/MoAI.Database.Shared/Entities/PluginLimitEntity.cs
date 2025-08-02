using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 插件使用量限制.
/// </summary>
public partial class PluginLimitEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 插件id.
    /// </summary>
    public int PluginId { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 限制的规则类型,每天/总额/有效期.
    /// </summary>
    public int RuleType { get; set; }

    /// <summary>
    /// 限制值.
    /// </summary>
    public int LimitValue { get; set; }

    /// <summary>
    /// 过期时间.
    /// </summary>
    public DateTimeOffset ExpirationTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
