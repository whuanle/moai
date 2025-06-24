// <copyright file="IClientInfoProvider.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Services;

public interface IClientInfoProvider
{
    MoAI.Infra.Models.ClientInfo GetClientInfo();
}