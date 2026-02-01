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
/// oauth2.0系统.
/// </summary>
public partial class OauthConnectionEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 认证名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 提供商.
    /// </summary>
    public string Provider { get; set; } = default!;

    /// <summary>
    /// 应用key.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    public string Secret { get; set; } = default!;

    /// <summary>
    /// 图标地址.
    /// </summary>
    public string IconUrl { get; set; } = default!;

    /// <summary>
    /// 登录跳转地址.
    /// </summary>
    public string AuthorizeUrl { get; set; } = default!;

    /// <summary>
    /// 发现端口.
    /// </summary>
    public string WellKnown { get; set; } = default!;

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
