// <copyright file="CrawleState.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Wiki.Models;

/// <summary>
/// 爬虫状态.
/// </summary>
public enum CrawleState
{
    /// <summary>
    /// 无状态.
    /// </summary>
    None = 0,

    /// <summary>
    /// 等待处理.
    /// </summary>
    Wait,

    /// <summary>
    /// 正在处理.
    /// </summary>
    Processing,

    /// <summary>
    /// 取消.
    /// </summary>
    Cancal,

    /// <summary>
    /// 成功.
    /// </summary>
    Successful,

    /// <summary>
    /// 失败.
    /// </summary>
    Failed
}