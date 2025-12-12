using Maomi;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.AzureOpenAI;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.AI.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.Azure)]
public class AzureTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    /// <inheritdoc/>
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig wikiConfig)
    {
        return kernelMemoryBuilder.WithAzureOpenAITextEmbeddingGeneration(new AzureOpenAIConfig
        {
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
            Deployment = endpoint.DeploymentName,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            Tokenizer = wikiConfig.EmbeddingModelTokenizer.ToJsonString(),
            MaxTokenTotal = endpoint.ContextWindowTokens,

            MaxEmbeddingBatchSize = wikiConfig.EmbeddingBatchSize,
            MaxRetries = wikiConfig.MaxRetries,
            EmbeddingDimensions = wikiConfig.EmbeddingDimensions,
        });
    }

    /// <inheritdoc/>
    public ITextEmbeddingGenerator GetTextEmbeddingGenerator(AiEndpoint endpoint)
    {
#pragma warning disable KMEXP01 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
        var client = new AzureOpenAITextEmbeddingGenerator(new AzureOpenAIConfig
        {
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
            Deployment = endpoint.DeploymentName,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            MaxTokenTotal = endpoint.ContextWindowTokens,
        });
        return client;
    }
}