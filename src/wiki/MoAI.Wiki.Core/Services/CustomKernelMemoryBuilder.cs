// <copyright file="CustomKernelMemoryBuilder.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;

namespace MaomiAI.Document.Core.Services;

// todo: 后续重构
/// <summary>
/// 构建 km.
/// </summary>
[InjectOnScoped]
public class CustomKernelMemoryBuilder
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomKernelMemoryBuilder"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public CustomKernelMemoryBuilder(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <summary>
    /// 配置向量模型.
    /// </summary>
    /// <param name="kernelMemoryBuilder"></param>
    /// <param name="endpoint"></param>
    /// <param name="wikiConfig"></param>
    public void ConfigEmbeddingModel(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, WikiConfig wikiConfig)
    {
        if (endpoint.AiModelType != AiModelType.Embedding)
        {
            throw new BusinessException("{0} 不支持 TextEmbeddingGeneration.", endpoint.Name);
        }

        if (endpoint.Provider == AiProvider.OpenAI)
        {
            kernelMemoryBuilder.WithOpenAITextEmbeddingGeneration(new OpenAIConfig
            {
                EmbeddingModel = endpoint.DeploymentName,
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,

                MaxEmbeddingBatchSize = wikiConfig.EmbeddingBatchSize,
                MaxRetries = wikiConfig.MaxRetries,
                EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
                EmbeddingDimensions = wikiConfig.EmbeddingDimensions,
                EmbeddingModelTokenizer = wikiConfig.EmbeddingModelTokenizer
            });
        }
        else if (endpoint.Provider == AiProvider.Azure)
        {
            kernelMemoryBuilder.WithAzureOpenAITextEmbeddingGeneration(new AzureOpenAIConfig
            {
                Deployment = endpoint.DeploymentName,
                Endpoint = endpoint.Endpoint,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                APIKey = endpoint.Key,
                APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,

                MaxEmbeddingBatchSize = wikiConfig.EmbeddingBatchSize,
                MaxRetries = wikiConfig.MaxRetries,
                MaxTokenTotal = endpoint.TextOutput,
                EmbeddingDimensions = wikiConfig.EmbeddingDimensions,
                Tokenizer = wikiConfig.EmbeddingModelTokenizer
            });
        }
        else
        {
            throw new BusinessException("暂不支持此模型供应商");
        }
    }
}