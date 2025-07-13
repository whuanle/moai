// <copyright file="OpenAiTextEmbeddingGeneration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.OpenAI)]
public class OpenAiTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, WikiConfig wikiConfig)
    {
        return kernelMemoryBuilder.WithOpenAITextEmbeddingGeneration(new OpenAIConfig
        {
            EmbeddingModel = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,

            MaxEmbeddingBatchSize = wikiConfig.EmbeddingBatchSize,
            MaxRetries = wikiConfig.MaxRetries,
            EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
            EmbeddingDimensions = wikiConfig.EmbeddingDimensions,
            EmbeddingModelTokenizer = wikiConfig.EmbeddingModelTokenizer
        });
    }
}
