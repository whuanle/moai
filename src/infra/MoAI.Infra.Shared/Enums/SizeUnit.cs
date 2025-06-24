// <copyright file="SizeUnit.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Enums;

/// <summary>
/// 大小单位.
/// </summary>
public enum SizeUnit : int
{
    /// <summary>
    /// Byte.
    /// </summary>
    B = 0,

    /// <summary>
    /// KB.
    /// </summary>
    KB,

    /// <summary>
    /// MB.
    /// </summary>
    MB,

    /// <summary>
    /// GB.
    /// </summary>
    GB,

    /// <summary>
    /// TB.
    /// </summary>
    TB,

    /// <summary>
    /// PB.
    /// </summary>
    PB
}