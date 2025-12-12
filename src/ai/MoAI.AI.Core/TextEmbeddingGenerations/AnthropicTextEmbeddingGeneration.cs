using Maomi;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.AzureOpenAI;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Infra.Extensions;

namespace MoAI.AI.TextEmbeddingGenerations;

[InjectOnScoped(ServiceKey = AiProvider.Anthropic)]
public class AnthropicTextEmbeddingGeneration : ITextEmbeddingGeneration
{
    /// <inheritdoc/>
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, AiEndpoint endpoint, EmbeddingConfig wikiConfig)
    {
        return kernelMemoryBuilder.WithAnthropicTextGeneration(new Microsoft.KernelMemory.AI.Anthropic.AnthropicConfig
        {
            TextModelName = endpoint.Name,
            Endpoint = endpoint.Endpoint,
            ApiKey = endpoint.Key,

            Tokenizer = wikiConfig.EmbeddingModelTokenizer.ToJsonString(),
            MaxTokenOut = endpoint.TextOutput,
        });
    }

    /// <inheritdoc/>
    public ITextEmbeddingGenerator GetTextEmbeddingGenerator(AiEndpoint endpoint)
    {
        throw new NotImplementedException();
    }
}