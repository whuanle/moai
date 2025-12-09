using Maomi;
using Microsoft.KernelMemory;
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
}