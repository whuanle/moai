using Maomi;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.AzureOpenAI;
using Microsoft.KernelMemory.AI.OpenAI;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.Wiki.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.OpenAI)]
public class OpenAiTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig wikiConfig)
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
            EmbeddingModelTokenizer = wikiConfig.EmbeddingModelTokenizer.ToJsonString()
        });
    }

    /// <inheritdoc/>
    public ITextEmbeddingGenerator GetTextEmbeddingGenerator(AiEndpoint endpoint)
    {
#pragma warning disable KMEXP01 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
        var client = new OpenAITextEmbeddingGenerator(new OpenAIConfig
        {
            EmbeddingModel = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            APIKey = endpoint.Key,

            EmbeddingModelMaxTokenTotal = endpoint.TextOutput
        });
        return client;
    }
}
