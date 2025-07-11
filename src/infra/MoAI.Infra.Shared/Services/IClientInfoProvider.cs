// <copyright file="IClientInfoProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Services;

/// <summary>
/// IClientInfoProvider.
/// </summary>
public interface IClientInfoProvider
{
    /// <summary>
    /// 获取客户端信息.
    /// </summary>
    /// <returns></returns>
    MoAI.Infra.Models.ClientInfo GetClientInfo();
}