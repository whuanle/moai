#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Models;

namespace MoAI.AI.Anthropic;

/// <summary>
/// 支持 Anthropic.
/// </summary>
internal static class AnthropicExtensions
{
    /// <summary>
    /// 增加 Anthropic 支持.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="endpoint"></param>
    /// <param name="httpClient"></param>
    /// <param name="serviceId"></param>
    /// <returns></returns>
    public static IServiceCollection AddAnthropicChatCompletion(this IServiceCollection services, AiEndpoint endpoint, HttpClient httpClient, string? serviceId = null)
    {
        services.AddKeyedSingleton<IChatCompletionService>(serviceId, (serviceProvider, _) =>
        {
            AnthropicClient client = new();
            IChatClient chatClient = client
                .WithOptions(options =>
                options with
                {
                    BaseUrl = new(endpoint.Endpoint),
                    Timeout = TimeSpan.FromSeconds(60),
                    APIKey = endpoint.Key,
                    HttpClient = httpClient
                })
                .AsIChatClient(endpoint.Name)
                .AsBuilder()

                // 使用 Kernel 的才能走函数过滤器
                .UseKernelFunctionInvocation()
                .Build();

            return chatClient.AsChatCompletionService();
        });

        return services;
    }

    /// <summary>
    /// 增加 Anthropic 支持.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="endpoint"></param>
    /// <param name="httpClient"></param>
    /// <param name="serviceId"></param>
    /// <returns></returns>
    public static IKernelBuilder AddAnthropicChatCompletion(this IKernelBuilder kernelBuilder, AiEndpoint endpoint, HttpClient httpClient, string? serviceId = null)
    {
        AddAnthropicChatCompletion(kernelBuilder.Services, endpoint, httpClient, serviceId);
        return kernelBuilder;
    }
}