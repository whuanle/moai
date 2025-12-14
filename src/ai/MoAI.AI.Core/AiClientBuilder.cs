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
using MoAI.AI.MemoryDb;
using MoAI.AI.Models;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using OpenAI;
using StackExchange.Redis;
using System.ClientModel;

namespace MoAI.AI;

[InjectOnScoped]
public class AiClientBuilder : IDisposable, IAiClientBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemOptions _systemOptions;
    private readonly ILoggerFactory _loggerFactory;

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
        var provider = endpoint.Provider;
        if (provider == AiProvider.Azure)
        {
            return new AzureOpenAITextEmbeddingGenerator(new AzureOpenAIConfig
            {
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,
                APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                Deployment = endpoint.DeploymentName,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                MaxTokenTotal = endpoint.ContextWindowTokens,

                EmbeddingDimensions = embeddingDimensions
            });
        }

        if (provider == AiProvider.OpenAI)
        {
            return new OpenAITextEmbeddingGenerator(new OpenAIConfig
            {
                EmbeddingModel = endpoint.Name,
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,

                EmbeddingModelMaxTokenTotal = endpoint.TextOutput,
                EmbeddingDimensions = embeddingDimensions
            });
        }

        if (provider == AiProvider.Custom)
        {
            return new AzureOpenAITextEmbeddingGenerator(new AzureOpenAIConfig
            {
                Endpoint = endpoint.Endpoint,
                APIKey = endpoint.Key,
                APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                Deployment = endpoint.DeploymentName,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                MaxTokenTotal = endpoint.ContextWindowTokens,

                EmbeddingDimensions = embeddingDimensions
            });
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
        if (memoryDbType == MemoryDbType.ElasticSearch)
        {
            var esOptions = new ElasticsearchConfig
            {
                Endpoint = _systemOptions.Wiki.ConnectionString,
                IndexPrefix = "moaiwiki",
                Password = _systemOptions.Wiki.Password!,
                UserName = _systemOptions.Wiki.UserName!,
            };

            return new ElasticsearchMemory(esOptions, textEmbeddingGenerator, client: null, _loggerFactory);
        }

        if (memoryDbType == MemoryDbType.Qdrant)
        {
            var qOptions = new QdrantConfig
            {
                Endpoint = _systemOptions.Wiki.ConnectionString,
                APIKey = _systemOptions.Wiki.Password!,
            };

            return new QdrantMemory(qOptions, textEmbeddingGenerator, _loggerFactory);
        }

        if (memoryDbType == MemoryDbType.Postgres)
        {
            var pgOptions = new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.ConnectionString,
                TableNamePrefix = "wiki_",
            };

            return new PostgresMemory(pgOptions, textEmbeddingGenerator, _loggerFactory);
        }

        if (memoryDbType == MemoryDbType.Redis)
        {
            var redisOptions = new RedisConfig
            {
                ConnectionString = _systemOptions.Wiki.ConnectionString
            };

            var connectionMultiplexer = ConnectionMultiplexer.Connect(_systemOptions.Wiki.ConnectionString);
            _disposables.Add(connectionMultiplexer);

            return new RedisMemory(redisOptions, connectionMultiplexer, textEmbeddingGenerator, _loggerFactory.CreateLogger<RedisMemory>());
        }

        if (memoryDbType == MemoryDbType.SQLServer)
        {
            var sqlOptions = new SqlServerConfig
            {
                ConnectionString = _systemOptions.Wiki.ConnectionString,
            };

            return new SqlServerMemory(sqlOptions, textEmbeddingGenerator, queryProvider: null, _loggerFactory);
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
