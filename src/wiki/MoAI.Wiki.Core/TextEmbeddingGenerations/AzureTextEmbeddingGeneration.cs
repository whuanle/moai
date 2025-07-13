// <copyright file="AzureTextEmbeddingGeneration.cs" company="MoAI">
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

[InjectOnScoped(ServiceKey = AiProvider.Azure)]
public class AzureTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    /// <inheritdoc/>
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, WikiConfig wikiConfig)
    {
        return kernelMemoryBuilder.WithAzureOpenAITextEmbeddingGeneration(new AzureOpenAIConfig
        {
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
            Deployment = endpoint.DeploymentName,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            Tokenizer = wikiConfig.EmbeddingModelTokenizer,
            MaxTokenTotal = endpoint.ContextWindowTokens,

            MaxEmbeddingBatchSize = wikiConfig.EmbeddingBatchSize,
            MaxRetries = wikiConfig.MaxRetries,
            EmbeddingDimensions = wikiConfig.EmbeddingDimensions,
        });
    }
}