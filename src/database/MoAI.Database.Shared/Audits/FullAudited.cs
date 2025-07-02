// <copyright file="FullAudited.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Database.Audits;

/// <summary>
/// 全部审计属性.
/// </summary>
public class FullAudited : IFullAudited
{
    /// <inheritdoc/>
    public virtual long IsDeleted { get; set; }

    /// <inheritdoc/>
    public virtual DateTimeOffset CreateTime { get; set; }

    /// <inheritdoc/>
    public virtual DateTimeOffset UpdateTime { get; set; }

    /// <inheritdoc/>
    public virtual int CreateUserId { get; set; }

    /// <inheritdoc/>
    public virtual int UpdateUserId { get; set; }
}