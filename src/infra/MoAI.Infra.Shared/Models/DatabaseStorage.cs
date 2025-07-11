// <copyright file="DatabaseStorage.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// DatabaseStore.
/// </summary>
public class DatabaseStorage
{
    /// <summary>
    /// 系统数据库类型.
    /// </summary>
    public string DBType { get; init; } = string.Empty;

    /// <summary>
    /// 系统数据库连接字符串.
    /// </summary>
    public string Database { get; init; } = string.Empty;
}