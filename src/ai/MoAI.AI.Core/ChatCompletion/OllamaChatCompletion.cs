
#pragma warning disable SKEXP0070 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
using Maomi;
using Microsoft.SemanticKernel;
using MoAI.AI.Abstract;
using MoAI.AI.Models;

namespace MoAI.AI.ChatCompletion;

[InjectOnScoped(ServiceKey = AiProvider.Ollama)]
public class OllamaChatCompletion : IChatCompletionConfigurator
{
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        return kernelBuilder.AddOllamaChatCompletion(modelId: endpoint.Name, new Uri(endpoint.Endpoint), "MoAI");
    }
}
