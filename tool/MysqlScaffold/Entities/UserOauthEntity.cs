// <copyright file="UserOauthEntity.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// oauth2.0对接.
/// </summary>
public partial class UserOauthEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 供应商id.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    public string Sub { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
