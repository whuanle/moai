using Maomi;
using Microsoft.SemanticKernel;
using MoAI.AI.Abstract;
using MoAI.AI.Models;
using OpenAI;
using System.ClientModel;

namespace MoAI.AI.ChatCompletion;

[InjectOnScoped(ServiceKey = AiProvider.Custom)]
public class CustomChatCompletion : IChatCompletionConfigurator
{
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        var openAIClientCredential = new ApiKeyCredential(endpoint.Key);
        var openAIClientOption = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint.Endpoint),
        };

        var openapiClient = new OpenAIClient(openAIClientCredential, openAIClientOption);
        return kernelBuilder
            .AddOpenAIChatCompletion(endpoint.Name, openapiClient, serviceId: "MoAI");
    }
}