// <copyright file="OpenAiTextEmbeddingGeneration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.Wiki.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.OpenAI)]
public class OpenAiTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig embeddingConfig)
    {
        return kernelMemoryBuilder.WithOpenAITextEmbeddingGeneration(new OpenAIConfig
        {
            EmbeddingModel = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,

            MaxEmbeddingBatchSize = embeddingConfig.EmbeddingBatchSize,
            MaxRetries = embeddingConfig.MaxRetries,
            EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
            EmbeddingDimensions = embeddingConfig.EmbeddingDimensions,
            EmbeddingModelTokenizer = embeddingConfig.EmbeddingModelTokenizer.ToJsonString()
        });
    }
}
