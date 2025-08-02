// <copyright file="CustomTextEmbeddingGeneration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable KMEXP01 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.OpenAI;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.AI.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.Custom)]
public class CustomTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    /// <inheritdoc/>
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig embeddingConfig)
    {
        var config = new OpenAIConfig
        {
            EmbeddingModel = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,

            MaxEmbeddingBatchSize = embeddingConfig.EmbeddingBatchSize,
            MaxRetries = embeddingConfig.MaxRetries,
            EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
            EmbeddingDimensions = embeddingConfig.EmbeddingDimensions,
            EmbeddingModelTokenizer = embeddingConfig.EmbeddingModelTokenizer.ToJsonString()
        };

        var embeddingGenerator = new OpenAITextEmbeddingGenerator(config);

        return kernelMemoryBuilder
                    .WithCustomEmbeddingGenerator(embeddingGenerator);
    }
}