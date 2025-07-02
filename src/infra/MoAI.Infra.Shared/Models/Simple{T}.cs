// <copyright file="Simple{T}.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// 简单类型.
/// </summary>
/// <typeparam name="T">任何类型.</typeparam>
public class Simple<T>
{
    /// <summary>
    /// 任何类型.
    /// </summary>
    public T Value { get; init; } = default!;
}
