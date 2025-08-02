// <copyright file="ITextEmbeddingGeneration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.KernelMemory;
using MoAI.AiModel.Models;

namespace MoAI.AiModel.Services;

public interface ITextEmbeddingGeneration
{
    IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig wikiConfig);
}
