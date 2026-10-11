using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        var inner = client.GetEmbeddingClient(model.ModelId).AsIEmbeddingGenerator();
        return inner.AsBuilder()
            .Use(inner => new EmbeddingParameterFallbackGenerator(inner, model, channel))
            .Build();
    }

    /// <summary>
    /// 向量化兼容兜底装饰器：部分模型服务（如腾讯云 kinfra 系 embedding 模型）不支持 dimensions 参数、或只接受
    /// encoding_format=float（OpenAI SDK 默认发送 base64）。命中这两类服务端报错时，绕过 SDK 直接调用渠道的
    /// /embeddings 接口——请求不带 dimensions、不带 encoding_format，由模型按原生维度输出。
    /// 注意：降级路径忽略调用方请求的维度，知识库/图谱维度需与模型原生维度一致（如 0.6b 模型为 1024）。
    /// </summary>
    private sealed class EmbeddingParameterFallbackGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _inner;
        private readonly AiModelEntity _model;
        private readonly AiChannelEntity _channel;

        public EmbeddingParameterFallbackGenerator(IEmbeddingGenerator<string, Embedding<float>> inner, AiModelEntity model, AiChannelEntity channel)
        {
            _inner = inner;
            _model = model;
            _channel = channel;
        }

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _inner.GenerateAsync(values, options, cancellationToken);
            }
            catch (Exception ex) when (options?.Dimensions != null && HasEmbeddingParameterRestriction(ex))
            {
                var results = new List<Embedding<float>>();
                foreach (var value in values)
                {
                    var vector = await GenerateVectorViaRawEmbeddingsAsync(_channel, _model, value, cancellationToken);
                    results.Add(new Embedding<float>(vector));
                }

                return new GeneratedEmbeddings<Embedding<float>>(results);
            }
        }

        private static bool HasEmbeddingParameterRestriction(Exception ex)
        {
            for (var e = (Exception?)ex; e != null; e = e.InnerException)
            {
                if (e.Message.Contains("dimensions is not supported", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("encoding_format only supports float", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly HttpClient RawEmbeddingHttpClient = new HttpClient();

        private static async Task<ReadOnlyMemory<float>> GenerateVectorViaRawEmbeddingsAsync(
            AiChannelEntity channel,
            AiModelEntity model,
            string content,
            CancellationToken cancellationToken)
        {
            var baseUrl = channel.BaseUrl?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new BusinessException("渠道未配置服务地址，无法执行向量降级调用.") { StatusCode = 400 };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/embeddings");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", channel.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { model = model.ModelId, input = new[] { content } }),
                Encoding.UTF8,
                "application/json");

            using var response = await RawEmbeddingHttpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new BusinessException($"向量降级调用失败：HTTP {(int)response.StatusCode} {payload}") { StatusCode = 400 };
            }

            using var document = JsonDocument.Parse(payload);
            return document.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .First()
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(x => x.GetSingle())
                .ToArray();
        }

        public EmbeddingGeneratorMetadata Metadata => (EmbeddingGeneratorMetadata?)_inner.GetService(typeof(EmbeddingGeneratorMetadata)) ?? new EmbeddingGeneratorMetadata("OpenAI-compatible", null, _model.ModelId);

        public object? GetService(global::System.Type serviceType, object? serviceKey = null) => _inner.GetService(serviceType, serviceKey);

        public void Dispose() => _inner.Dispose();
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
