// <copyright file="FileStoreMode.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Store.Enums;

/// <summary>
/// 文件存储模式.
/// </summary>
public enum FileStoreMode
{
    /// <summary>
    /// 本地存储.
    /// </summary>
    Local = 0,

    /// <summary>
    /// S3 存储.
    /// </summary>
    S3 = 1
}