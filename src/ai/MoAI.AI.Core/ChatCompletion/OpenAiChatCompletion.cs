using Maomi;
using Microsoft.SemanticKernel;
using MoAI.AI.Abstract;
using MoAI.AiModel.Models;

namespace MoAI.AI.ChatCompletion;

[InjectOnScoped(ServiceKey = AiProvider.OpenAI)]
public class OpenAiChatCompletion : IChatCompletionConfigurator
{
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        return kernelBuilder.AddOpenAIChatCompletion(
            apiKey: endpoint.Key,
            endpoint: new Uri(endpoint.Endpoint),
            modelId: endpoint.Name,
            serviceId: "MoAI");
    }
}
