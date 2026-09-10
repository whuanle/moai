using System;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using Mscc.GenerativeAI.Microsoft;
using OpenAI;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 依据渠道协议族，为不同模型构建统一的 <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> 与 <see cref="IChatClient"/>.
/// </summary>
public class AIClientProvider : IEmbeddingGeneratorProvider, IChatClientProvider
{
    private readonly ILogger<AIClientProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIClientProvider"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public AIClientProvider(ILogger<AIClientProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(AiModelEntity model, AiChannelEntity channel, CancellationToken cancellationToken = default)
    {
        var protocol = (AIProtocolFamily)channel.ProtocolFamily;
        return protocol switch
        {
            AIProtocolFamily.OpenAIChatCompletions or AIProtocolFamily.OpenAIResponses
                => Task.FromResult(BuildOpenAIEmbeddingGenerator(model, channel)),
            AIProtocolFamily.GoogleGemini
                => Task.FromResult(BuildGeminiEmbeddingGenerator(model, channel)),
            _ => throw new BusinessException($"渠道协议 {protocol} 不支持向量化.") { StatusCode = 400 },
        };
    }

    /// <inheritdoc/>
    public Task<IChatClient> GetChatClientAsync(AiModelEntity model, AiChannelEntity channel, CancellationToken cancellationToken = default)
    {
        var protocol = (AIProtocolFamily)channel.ProtocolFamily;
        return protocol switch
        {
            AIProtocolFamily.OpenAIChatCompletions or AIProtocolFamily.OpenAIResponses
                => Task.FromResult(BuildOpenAIChatClient(model, channel)),
            AIProtocolFamily.AnthropicMessages
                => Task.FromResult(BuildAnthropicChatClient(model, channel)),
            AIProtocolFamily.GoogleGemini
                => Task.FromResult(BuildGeminiChatClient(model, channel)),
            _ => throw new BusinessException($"渠道协议 {protocol} 暂不支持对话调用.") { StatusCode = 400 },
        };
    }

    private static IEmbeddingGenerator<string, Embedding<float>> BuildOpenAIEmbeddingGenerator(AiModelEntity model, AiChannelEntity channel)
    {
        var client = CreateOpenAIClient(channel);
        return client.GetEmbeddingClient(model.ModelId).AsIEmbeddingGenerator();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> BuildGeminiEmbeddingGenerator(AiModelEntity model, AiChannelEntity channel)
    {
        _ = model;
        return new GeminiEmbeddingGenerator(channel.ApiKey, model.ModelId, NullLogger.Instance);
    }

    private static IChatClient BuildOpenAIChatClient(AiModelEntity model, AiChannelEntity channel)
    {
        var client = CreateOpenAIClient(channel);
        return client.GetChatClient(model.ModelId).AsIChatClient();
    }

    private static IChatClient BuildAnthropicChatClient(AiModelEntity model, AiChannelEntity channel)
    {
        var options = new Anthropic.Core.ClientOptions
        {
            ApiKey = channel.ApiKey,
        };
        if (!string.IsNullOrWhiteSpace(channel.BaseUrl))
        {
            options.BaseUrl = channel.BaseUrl.TrimEnd('/');
        }

        return new AnthropicClient(options).AsIChatClient(model.ModelId);
    }

    private static IChatClient BuildGeminiChatClient(AiModelEntity model, AiChannelEntity channel)
    {
        var httpOptions = string.IsNullOrWhiteSpace(channel.BaseUrl)
            ? null
            : new HttpOptions { BaseUrl = channel.BaseUrl.TrimEnd('/') };
        return new Client(apiKey: channel.ApiKey, httpOptions: httpOptions).AsIChatClient(model.ModelId);
    }

    private static OpenAIClient CreateOpenAIClient(AiChannelEntity channel)
    {
        var credential = new System.ClientModel.ApiKeyCredential(channel.ApiKey);
        if (string.IsNullOrWhiteSpace(channel.BaseUrl))
        {
            return new OpenAIClient(credential);
        }

        return new OpenAIClient(credential, new OpenAIClientOptions
        {
            Endpoint = new Uri(channel.BaseUrl.TrimEnd('/')),
        });
    }
}
