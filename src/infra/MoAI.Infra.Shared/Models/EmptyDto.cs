// <copyright file="EmptyDto.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CA1052 // 静态容器类型应为 Static 或 NotInheritable

namespace MoAI.Infra.Models;

/// <summary>
/// 空数据.
/// </summary>
public class EmptyDto
{
    /// <summary>
    /// 默认实例.
    /// </summary>
    public static readonly EmptyDto Default = new EmptyDto();
}
