// <copyright file="IMemoryD
// bClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.KernelMemory;

namespace MoAI.AiModel.Services;

public interface IMemoryDbClient
{
    IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString);
}