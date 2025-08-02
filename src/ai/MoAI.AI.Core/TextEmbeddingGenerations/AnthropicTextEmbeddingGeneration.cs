// <copyright file="AzureTextEmbeddingGeneration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.AI.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.Anthropic)]
public class AnthropicTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    /// <inheritdoc/>
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig embeddingConfig)
    {
        return kernelMemoryBuilder.WithAnthropicTextGeneration(new Microsoft.KernelMemory.AI.Anthropic.AnthropicConfig
        {
            TextModelName = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            ApiKey = endpoint.Key,

            Tokenizer = embeddingConfig.EmbeddingModelTokenizer.ToJsonString(),
            MaxTokenOut = endpoint.TextOutput,
        });
    }
}