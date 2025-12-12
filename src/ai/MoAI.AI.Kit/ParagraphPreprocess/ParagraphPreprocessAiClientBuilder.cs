using Maomi;
using MoAI.AI.Models;

namespace MoAI.AI.ParagraphPreprocess;

[InjectOnScoped]
public class ParagraphPreprocessAiClientBuilder : IParagraphPreprocessBuilder
{
    private readonly IServiceProvider _serviceProvider;

    public ParagraphPreprocessAiClientBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public IDocumentPreprocessor GetDocumentPreprocessor(AiEndpoint chat, AiEndpoint embedding)
    {
        var client = new ParagraphPreprocessAiClient(chat, embedding, _serviceProvider);
        return new DocumentPreprocessor(client);
    }
}
