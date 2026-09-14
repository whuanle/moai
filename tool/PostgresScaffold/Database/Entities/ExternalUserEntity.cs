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
/// 外部用户（外部应用接入的身份记录）.
/// </summary>
public partial class ExternalUserEntity : IFullAudited
{
    /// <summary>
    /// 外部用户id，自增主键，承载会话 create_user_id 与用量 user_id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 归属团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 授权访问的应用id，is_auth=false 匿名访问时为来源应用，用户 token 指定授权的单个应用.
    /// </summary>
    public Guid? AppId { get; set; }

    /// <summary>
    /// 来源应用接入id（access_app.key 换取 token 时写入），匿名访问为 null.
    /// </summary>
    public Guid? AccessAppId { get; set; }

    /// <summary>
    /// 外部身份标识，第三方系统的用户唯一标识或临时随机值.
    /// </summary>
    public string ExternalUserId { get; set; } = default!;

    /// <summary>
    /// 外部用户显示名，可选.
    /// </summary>
    public string? Nickname { get; set; }

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

    public DateTimeOffset UpdateTime { get; set; }

    public long IsDeleted { get; set; }
}
