// <copyright file="ICreationAudited.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Database.Audits;

/// <summary>
/// 创建审计属性.
/// </summary>
public interface ICreationAudited
{
    /// <summary>
    /// 创建人的用户名.
    /// </summary>
    int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    DateTimeOffset CreateTime { get; set; }
}