
#pragma warning disable KMEXP01 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
using Maomi;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.AzureOpenAI;
using Microsoft.KernelMemory.AI.OpenAI;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.AI.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.Custom)]
public class CustomTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    /// <inheritdoc/>
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig wikiConfig)
    {
        var config = new OpenAIConfig
        {
            EmbeddingModel = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,

            MaxEmbeddingBatchSize = wikiConfig.EmbeddingBatchSize,
            MaxRetries = wikiConfig.MaxRetries,
            EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
            EmbeddingDimensions = wikiConfig.EmbeddingDimensions,
            EmbeddingModelTokenizer = wikiConfig.EmbeddingModelTokenizer.ToJsonString()
        };

        var embeddingGenerator = new OpenAITextEmbeddingGenerator(config);

        return kernelMemoryBuilder
                    .WithCustomEmbeddingGenerator(embeddingGenerator);
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