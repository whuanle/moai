#pragma warning disable KMEXP01 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable KMEXP03 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0070 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.AzureOpenAI;
using Microsoft.KernelMemory.AI.OpenAI;
using Microsoft.KernelMemory.MemoryDb.Elasticsearch;
using Microsoft.KernelMemory.MemoryDb.Qdrant;
using Microsoft.KernelMemory.MemoryDb.Redis;
using Microsoft.KernelMemory.MemoryDb.SQLServer;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Postgres;
using Microsoft.SemanticKernel;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Models;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using OpenAI;
using StackExchange.Redis;
using System.ClientModel;
using System.Collections.Concurrent;

namespace MoAI.AI;

/// <summary>
/// AiClientBuilder.
/// </summary>
[InjectOnScoped]
public class AiClientBuilder : IDisposable, IAiClientBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemOptions _systemOptions;
    private readonly ILoggerFactory _loggerFactory;

    private readonly ConcurrentDictionary<AiEndpoint, ITextEmbeddingGenerator> _textEmbeddingGenerators = new();
    private readonly ConcurrentDictionary<ITextEmbeddingGenerator, ConcurrentBag<IMemoryDb>> _memoryDBs = new();

    private readonly List<IDisposable> _disposables = new();
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiClientBuilder"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="systemOptions"></param>
    /// <param name="loggerFactory"></param>
    public AiClientBuilder(IServiceProvider serviceProvider, SystemOptions systemOptions, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _systemOptions = systemOptions;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        var provider = endpoint.Provider;

        if (provider == AiProvider.Azure)
        {
            return kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: endpoint.DeploymentName,
                    apiKey: endpoint.Key,
                    endpoint: endpoint.Endpoint,
                    modelId: endpoint.Name,
                    serviceId: "MoAI");
        }

        if (provider == AiProvider.Custom)
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

        if (provider == AiProvider.Google)
        {
            return kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId: endpoint.Name, apiKey: endpoint.Key, serviceId: "MoAI");
        }

        if (provider == AiProvider.HuggingFace)
        {
            return kernelBuilder.AddHuggingFaceChatCompletion(model: endpoint.Name, endpoint: new Uri(endpoint.Endpoint), apiKey: endpoint.Key, serviceId: "MoAI");
        }

        if (provider == AiProvider.Mistral)
        {
            return kernelBuilder.AddMistralChatCompletion(modelId: endpoint.Name, endpoint.Key, new Uri(endpoint.Endpoint), "MoAI");
        }

        if (provider == AiProvider.Ollama)
        {
            return kernelBuilder.AddOllamaChatCompletion(modelId: endpoint.Name, new Uri(endpoint.Endpoint), "MoAI");
        }

        if (provider == AiProvider.OpenAI)
        {
            return kernelBuilder.AddOpenAIChatCompletion(
                apiKey: endpoint.Key,
                endpoint: new Uri(endpoint.Endpoint),
                modelId: endpoint.Name,
                serviceId: "MoAI");
        }

        throw new BusinessException("不支持该模型接口");
    }

    /// <inheritdoc/>
    public ITextEmbeddingGenerator CreateTextEmbeddingGenerator(AiEndpoint endpoint, int embeddingDimensions)
    {
        var cache = _textEmbeddingGenerators.FirstOrDefault(x =>
        x.Key.Provider == endpoint.Provider &&
        x.Key.Name == endpoint.Name &&
        x.Key.Endpoint == endpoint.Endpoint);

        if (cache.Value != null)
        {
            return cache.Value;
        }

        var provider = endpoint.Provider;
        if (provider == AiProvider.Azure)
        {
            var obj = new AzureOpenAITextEmbeddingGenerator(new AzureOpenAIConfig
            {
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,
                APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                Deployment = endpoint.DeploymentName,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                MaxTokenTotal = endpoint.ContextWindowTokens,

                EmbeddingDimensions = embeddingDimensions
            });

            _textEmbeddingGenerators[endpoint] = obj;
            return obj;
        }

        if (provider == AiProvider.OpenAI)
        {
            var obj = new OpenAITextEmbeddingGenerator(new OpenAIConfig
            {
                EmbeddingModel = endpoint.Name,
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,

                EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
                EmbeddingDimensions = embeddingDimensions
            });

            _textEmbeddingGenerators[endpoint] = obj;
            return obj;
        }

        if (provider == AiProvider.Custom)
        {
            var obj = new AzureOpenAITextEmbeddingGenerator(new AzureOpenAIConfig
            {
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,
                APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                Deployment = endpoint.DeploymentName,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                MaxTokenTotal = endpoint.ContextWindowTokens,

                EmbeddingDimensions = embeddingDimensions
            });

            _textEmbeddingGenerators[endpoint] = obj;
            return obj;
        }

        throw new BusinessException("不支持该模型接口");
    }

    /// <inheritdoc/>
    public IMemoryDb CreateMemoryDb(AiEndpoint endpoint, MemoryDbType memoryDbType, int embeddingDimensions)
    {
        var textEmbeddingGenerator = CreateTextEmbeddingGenerator(endpoint, embeddingDimensions);
        return CreateMemoryDb(textEmbeddingGenerator, memoryDbType);
    }

    /// <inheritdoc/>
    public IMemoryDb CreateMemoryDb(ITextEmbeddingGenerator textEmbeddingGenerator, MemoryDbType memoryDbType)
    {
        if (!_memoryDBs.TryGetValue(textEmbeddingGenerator, out var values))
        {
            values = new ConcurrentBag<IMemoryDb>();
            _memoryDBs[textEmbeddingGenerator] = values;
        }

        if (memoryDbType == MemoryDbType.ElasticSearch)
        {
            var value = values.FirstOrDefault(x => x is ElasticsearchMemory);
            if (value != null)
            {
                return value;
            }

            var esOptions = new ElasticsearchConfig
            {
                Endpoint = _systemOptions.Wiki.ConnectionString,
                IndexPrefix = "moaiwiki",
                Password = _systemOptions.Wiki.Password!,
                UserName = _systemOptions.Wiki.UserName!,
            };

            var memory = new ElasticsearchMemory(esOptions, textEmbeddingGenerator, client: null, _loggerFactory);
            values.Add(memory);
            return memory;
        }

        if (memoryDbType == MemoryDbType.Qdrant)
        {
            var value = values.FirstOrDefault(x => x is QdrantMemory);
            if (value != null)
            {
                return value;
            }

            var qOptions = new QdrantConfig
            {
                Endpoint = _systemOptions.Wiki.ConnectionString,
                APIKey = _systemOptions.Wiki.Password!,
            };

            var memory = new QdrantMemory(qOptions, textEmbeddingGenerator, _loggerFactory);
            values.Add(memory);
            return memory;
        }

        if (memoryDbType == MemoryDbType.Postgres)
        {
            var value = values.FirstOrDefault(x => x is PostgresMemory);
            if (value != null)
            {
                return value;
            }

            var pgOptions = new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.ConnectionString,
                TableNamePrefix = "wiki_",
            };

            var memory = new PostgresMemory(pgOptions, textEmbeddingGenerator, _loggerFactory);
            values.Add(memory);
            return memory;
        }

        if (memoryDbType == MemoryDbType.Redis)
        {
            var value = values.FirstOrDefault(x => x is RedisMemory);
            if (value != null)
            {
                return value;
            }

            var redisOptions = new RedisConfig
            {
                ConnectionString = _systemOptions.Wiki.ConnectionString
            };

            var connectionMultiplexer = ConnectionMultiplexer.Connect(_systemOptions.Wiki.ConnectionString);
            _disposables.Add(connectionMultiplexer);

            var memory = new RedisMemory(redisOptions, connectionMultiplexer, textEmbeddingGenerator, _loggerFactory.CreateLogger<RedisMemory>());
            values.Add(memory);
            return memory;
        }

        if (memoryDbType == MemoryDbType.SQLServer)
        {
            var value = values.FirstOrDefault(x => x is SqlServerMemory);
            if (value != null)
            {
                return value;
            }

            var sqlOptions = new SqlServerConfig
            {
                ConnectionString = _systemOptions.Wiki.ConnectionString,
            };

            var memory = new SqlServerMemory(sqlOptions, textEmbeddingGenerator, queryProvider: null, _loggerFactory);
            values.Add(memory);
            return memory;
        }

        throw new BusinessException("不支持该存储类型");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                foreach (var item in _disposables)
                {
                    item.Dispose();
                }
            }

            disposedValue = true;
        }
    }
}
