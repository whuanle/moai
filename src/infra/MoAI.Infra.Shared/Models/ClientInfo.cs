// <copyright file="ClientInfo.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// 客户端信息.
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// IP.
    /// </summary>
    public string IP { get; init; } = string.Empty;

    /// <summary>
    /// UserAgent.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;
}
