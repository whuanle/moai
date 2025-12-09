using Maomi;
using Microsoft.KernelMemory;
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
}
