using System;
using System.Collections.Generic;
using MoAI.Database.Audits;
using MoAI.Database.Enums;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 子系统：独立部署、通过 appid+密钥接入 MoAI 的外部子系统（如智能报表），用户体系由 MoAI 承载.
/// </summary>
public partial class SubsystemEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 子系统标识（appid），创建后不可修改，子系统凭此换取接入 token.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 子系统名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 接入密钥哈希（PBKDF2），原文仅在创建/重置时返回一次.
    /// </summary>
    public string Secret { get; set; } = default!;

    /// <summary>
    /// 接入密钥盐（Base64）.
    /// </summary>
    public string SecretSalt { get; set; } = default!;

    /// <summary>
    /// 状态：0=启用 1=停用，停用后拒绝换取 token 与机器面调用.
    /// </summary>
    public SubsystemStatus Status { get; set; }

    /// <summary>
    /// 团队授权范围：0=全员可用 1=指定团队.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; set; }

    /// <summary>
    /// 委托 SSO 登录回调地址（子系统侧完整 URL），空串=未开放登录接入.
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

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
