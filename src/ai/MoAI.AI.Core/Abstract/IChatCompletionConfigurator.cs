﻿// <copyright file="IChatCompletionConfigurator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.SemanticKernel;
using MoAI.AiModel.Models;

namespace MoAI.AI.Abstract;

/// <summary>
/// 构建聊天完成配置器接口.
/// </summary>
public interface IChatCompletionConfigurator
{
    /// <summary>
    /// 配置客户端.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint);
}
