// <copyright file="IDeleteAudited.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace MoAI.Database.Audits;

/// <summary>
/// 删除审计属性.
/// </summary>
public interface IDeleteAudited : IModificationAudited
{
    /// <summary>
    /// 是否删除.
    /// </summary>
    [Required]
    long IsDeleted { get; set; }
}