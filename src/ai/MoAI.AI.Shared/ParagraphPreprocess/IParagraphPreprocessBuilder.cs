using MoAI.AI.Models;

namespace MoAI.AI.ParagraphPreprocess;

public interface IParagraphPreprocessBuilder
{
    IDocumentPreprocessor GetDocumentPreprocessor(AiEndpoint chat, AiEndpoint embedding);
}