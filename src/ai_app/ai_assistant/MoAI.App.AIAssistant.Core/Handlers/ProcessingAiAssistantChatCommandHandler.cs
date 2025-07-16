// <copyright file="ProcessingAiAssistantChatCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

using MaomiAI.Chat.Core.Handlers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Storage.Queries;
using MoAI.Store.Enums;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;
using ModelContextProtocol.Client;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// <inheritdoc cref="ProcessingAiAssistantChatCommand"/>
/// </summary>
public class ProcessingAiAssistantChatCommandHandler : IStreamRequestHandler<ProcessingAiAssistantChatCommand, IOpenAIChatCompletionsObject>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IMediator _mediator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingAiAssistantChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="mediator"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="systemOptions"></param>
    public ProcessingAiAssistantChatCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, IRedisDatabase redisDatabase, IMediator mediator, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, SystemOptions systemOptions)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _mediator = mediator;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IOpenAIChatCompletionsObject> Handle(ProcessingAiAssistantChatCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // todo: 暂时限制最大 5 个插件
        // todo: 支持函数调用前端输出提示
        Guid chatId = default!;
        ChatHistory chatMessages = new();

        if (request.ChatId == null || request.ChatId.Value == default)
        {
            var createResult = await _mediator.Send(
                new CreateAiAssistantChatCommand
                {
                    Title = request.Title,
                    ModelId = request.ModelId,
                    PluginIds = request.PluginIds,
                    ExecutionSettings = request.ExecutionSettings,
                    WikiId = request.WikiId,
                },
                cancellationToken);

            chatId = createResult.ChatId;
        }
        else
        {
            chatId = request.ChatId!.Value;
        }

        var chatObjectEntity = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == chatId && x.CreateUserId == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatObjectEntity == null)
        {
            throw new BusinessException("对话不存在或无权访问") { StatusCode = 404 };
        }

        await UpdateChatObjectEntityAsync(request, chatObjectEntity);

        // 添加提示词.
        if (!string.IsNullOrEmpty(request.Prompt))
        {
            chatMessages.AddSystemMessage(request.Prompt);
        }

        // 补全对话上下文
        var history = await _databaseContext.AppAssistantChatHistories
            .Where(x => x.ChatId == chatId)
            .OrderBy(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        foreach (var item in history)
        {
            if (item.Role == AuthorRole.User.Label)
            {
                chatMessages.AddAssistantMessage(item.Content);
            }
            else if (item.Role == AuthorRole.Assistant.Label)
            {
                chatMessages.AddAssistantMessage(item.Content);
            }
            else if (item.Role == AuthorRole.System.Label)
            {
                chatMessages.AddSystemMessage(item.Content);
            }
            else
            {
                // 其他角色不处理
                continue;
            }
        }

        chatMessages.AddUserMessage(request.Content);

        // 要给 AI 调用的插件列表
        List<KernelPlugin> aiPlugins = new List<KernelPlugin>();

        var pluginIds = request.PluginIds.Where(x => x > 0).ToHashSet();

        if (pluginIds.Count > 0)
        {
            await BuildFunctionPluginAsync(request, aiPlugins, pluginIds, cancellationToken);
        }

        if (request.WikiId > 0)
        {
            await BuildWikiPluginAsync(request, aiPlugins, cancellationToken);
        }

        var aiEndpoint = await _databaseContext.AiModels
            .Where(x => x.Id == request.ModelId)
            .Select(x => new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = Enum.Parse<AiModelType>(x.AiModelType, true),
                Provider = Enum.Parse<AiProvider>(x.AiProvider, true),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Key = x.Key,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (aiEndpoint == null)
        {
            throw new BusinessException("未找到模型") { StatusCode = 404 };
        }

        if (aiEndpoint.AiModelType != AiModelType.Chat)
        {
            throw new BusinessException("该模型不支持对话") { StatusCode = 400 };
        }

        var command = new ChatCompletionsCommand
        {
            ChatId = chatId,
            ChatHistory = chatMessages,
            Plugins = aiPlugins,
            Endpoint = aiEndpoint,
            ExecutionSetting = request.ExecutionSettings
        };

        OpenAIChatCompletionsObject completionsObject = default!;

        await foreach (var item in _mediator.CreateStream(command, cancellationToken))
        {
            // 聊天块内容
            if (item is OpenAIChatCompletionsChunk chunk)
            {
            }

            // 结束聊天
            else if (item is OpenAIChatCompletionsObject chatObject)
            {
                chatObjectEntity.InputTokens += chatObject.Usage.PromptTokens;
                chatObjectEntity.OutTokens += chatObject.Usage.CompletionTokens;
                chatObjectEntity.TotalTokens += chatObject.Usage.TotalTokens;

                completionsObject = chatObject;
            }
            else
            {
            }

            yield return item;
        }

        _databaseContext.Update(chatObjectEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var userChatRecord = new AppAssistantChatHistoryEntity
        {
            ChatId = chatId,
            CompletionsId = completionsObject.Id,
            Content = request.Content,
            Model = completionsObject.Model,
            Role = AuthorRole.User.Label
        };

        var aiChatRecord = new AppAssistantChatHistoryEntity
        {
            ChatId = chatId,
            CompletionsId = completionsObject.Id,
            Content = completionsObject.Choices.FirstOrDefault()?.Message.Content ?? string.Empty,
            Model = completionsObject.Model,
            Role = AuthorRole.Assistant.Label
        };
    }

    private async Task BuildFunctionPluginAsync(ProcessingAiAssistantChatCommand request, List<KernelPlugin> aiPlugins, HashSet<int> pluginIds, CancellationToken cancellationToken)
    {
        var pluginEntities = await _databaseContext.Plugins.Where(x => pluginIds.Contains(x.Id) && (x.CreateUserId == request.UserId || x.IsPublic)).ToListAsync(cancellationToken);

        var pluginFunctionEntities = await _databaseContext.PluginFunctions
            .Where(x => pluginIds.Contains(x.PluginId))
            .GroupBy(x => x.PluginId).ToListAsync(cancellationToken);

        Dictionary<PluginEntity, IReadOnlyCollection<PluginFunctionEntity>> pluginLists = new();
        foreach (var plugin in pluginEntities)
        {
            var pluginFunctions = pluginFunctionEntities.FirstOrDefault(x => x.Key == plugin.Id)?.ToArray();
            if (pluginFunctions != null && pluginFunctions.Length > 0)
            {
                pluginLists.Add(plugin, pluginFunctions);
            }
        }

        // 翻译插件为 SK 支持的格式
        foreach (var item in pluginLists)
        {
            if (item.Key.Type == (int)PluginType.Mcp)
            {
                var plugin = await GetMCPPluginsAsync(item.Key, item.Value);
                aiPlugins.Add(plugin);
            }
            else if (item.Key.Type == (int)PluginType.OpenApi)
            {
                var plugin = await GetOpenApiPluginsAsync(item.Key, item.Value);
                aiPlugins.Add(plugin);
            }
            else if (item.Key.Type == (int)PluginType.System)
            {
                //// 系统插件
                //var systemFunctions = item.Value.Select(x => x.AsKernelFunction()).ToList();
                //functions.AddRange(systemFunctions);
            }
        }
    }

    private async Task UpdateChatObjectEntityAsync(ProcessingAiAssistantChatCommand request, AppAssistantChatEntity chatEntity)
    {
        chatEntity.Title = request.Title;
        chatEntity.ExecutionSettings = request.ExecutionSettings.ToJsonString();
        chatEntity.PluginIds = request.PluginIds.ToJsonString();
        chatEntity.ModelId = request.ModelId;
        chatEntity.WikiId = request.WikiId ?? 0;

        if (request.Prompt != null)
        {
            chatEntity.Prompt = request.Prompt;
        }

        _databaseContext.Update(chatEntity);
        await _databaseContext.SaveChangesAsync();
    }

    // 将知识库转换为插件
    private async Task BuildWikiPluginAsync(ProcessingAiAssistantChatCommand request, List<KernelPlugin> aiPlugins, CancellationToken cancellationToken)
    {
        // 读取知识库
        var wikiEntity = await _databaseContext
            .Wikis.Where(x => x.Id == request.WikiId! && (x.CreateUserId == request.UserId || _databaseContext.WikiUsers.Where(a => a.UserId == request.UserId).Any()))
            .FirstOrDefaultAsync(cancellationToken);

        if (wikiEntity == null)
        {
            throw new BusinessException("知识库不存在或无权访问");
        }

        // 获取知识库配置信息
        if (wikiEntity.EmbeddingModelId == 0)
        {
            throw new BusinessException("知识库系统配置错误");
        }

        // 该知识库使用的模型
        // todo： 后续判断知识库是否公开等
        var wikiAiModel = await _databaseContext.AiModels.Where(x => x.Id == wikiEntity.EmbeddingModelId).FirstOrDefaultAsync(cancellationToken);
        if (wikiAiModel == null)
        {
            throw new BusinessException("知识库模型向量配置错误");
        }

        var aiEndpoint = new AiEndpoint
        {
            Name = wikiAiModel.Name,
            DeploymentName = wikiAiModel.DeploymentName,
            Title = wikiAiModel.Title,
            AiModelType = Enum.Parse<AiModelType>(wikiAiModel.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(wikiAiModel.AiProvider, true),
            ContextWindowTokens = wikiAiModel.ContextWindowTokens,
            Endpoint = wikiAiModel.Endpoint,
            Key = wikiAiModel.Key,
            Abilities = new ModelAbilities
            {
                Files = wikiAiModel.Files,
                FunctionCall = wikiAiModel.FunctionCall,
                ImageOutput = wikiAiModel.ImageOutput,
                Vision = wikiAiModel.IsVision,
            },
            MaxDimension = wikiAiModel.MaxDimension,
            TextOutput = wikiAiModel.TextOutput
        };

        var wikiConfig = new WikiConfig
        {
            EmbeddingDimensions = wikiEntity.EmbeddingDimensions,
            EmbeddingBatchSize = wikiEntity.EmbeddingBatchSize,
            MaxRetries = wikiEntity.MaxRetries,
            EmbeddingModelTokenizer = wikiEntity.EmbeddingModelTokenizer,
        };

        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder()
            .WithSimpleFileStorage(Path.GetTempPath());

        var textEmbeddingGeneration = _serviceProvider.GetKeyedService<ITextEmbeddingGeneration>(wikiAiModel.AiProvider) ?? throw new BusinessException("知识库不支持该模型服务商");
        textEmbeddingGeneration.Configure(memoryBuilder, aiEndpoint, wikiConfig);

        var memoryClient = memoryBuilder.WithoutTextGenerator()
            .WithPostgresMemoryDb(new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.Database,
            })
            .Build();

        var memoryPlugin = new MemoryPlugin(defaultIndex: "n" + wikiEntity.Id.ToString(), memoryClient: memoryClient, waitForIngestionToComplete: true);
        var wikiPlugin = KernelPluginFactory.CreateFromObject(target: memoryPlugin, "KnowledgeMemory");

        aiPlugins.Add(wikiPlugin);
    }

    private async Task<KernelPlugin> GetOpenApiPluginsAsync(PluginEntity pluginEntity, IReadOnlyCollection<PluginFunctionEntity> functionEntities)
    {
        // 后续抽到一个方法命令中
        var headers = pluginEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!;

        // 从 oss 读取 swaggger 文件，如果相同的 md5 可能本地有缓存，则直接读取缓存
        var fileEntity = await _databaseContext.Files
            .Where(x => x.Id == pluginEntity.OpenapiFileId)
            .FirstOrDefaultAsync();

        if (fileEntity == null)
        {
            throw new BusinessException("插件{0}已失效", pluginEntity.Title) { StatusCode = 409 };
        }

        var filePath = await _mediator.Send(new QueryFileLocalPathCommand
        {
            ObjectKey = fileEntity.ObjectKey,
            Visibility = FileVisibility.Private
        });

        OpenApiDocumentParser parser = new();
        using FileStream stream = File.OpenRead(filePath.FilePath);
        RestApiSpecification specification = await parser.ParseAsync(stream);
        // var operations = specification.Operations.Where(x => pluginEntities.Any(p => p.Path == x.Path)).ToArray();

        var httpClient = _httpClientFactory.CreateClient("OpenApiClient");

        foreach (var header in headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        KernelPlugin plugin = OpenApiKernelPluginFactory.CreateFromOpenApi(
            pluginName: pluginEntity.PluginName,
            specification: specification,
            executionParameters: new OpenApiFunctionExecutionParameters()
            {
                EnablePayloadNamespacing = true,
                ServerUrlOverride = new Uri(pluginEntity.Server),
                LoggerFactory = _loggerFactory,
                OperationSelectionPredicate = (operation) =>
                {
                    // 只使用指定的插件
                    return functionEntities.Any(p => p.Name == operation.Id || p.Path == operation.Path);
                },
                HttpClient = httpClient,
            });

        return plugin;
    }

    private async Task<KernelPlugin> GetMCPPluginsAsync(PluginEntity pluginEntity, IReadOnlyCollection<PluginFunctionEntity> pluginFunctionEntities)
    {
        // 后续抽到一个方法命令中
        var headers = pluginEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!;
        var queries = pluginEntity.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>()!;

        // 第一步：创建 mcp 客户端
        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "MoAI", Version = "1.0.0" }
        };

        var uriBuilder = new UriBuilder(pluginEntity.Server);
        if (queries != null && queries.Count > 0)
        {
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            foreach (var kv in queries)
            {
                query[kv.Key] = kv.Value;
            }

            uriBuilder.Query = query.ToString();
        }

        var serverUrl = uriBuilder.Uri;
        var defaultConfig = new SseClientTransportOptions
        {
            Endpoint = serverUrl,
            Name = pluginEntity.Title,
            AdditionalHeaders = headers.ToDictionary(x => x.Key, x => x.Value),
        };

        await using var sseTransport = new SseClientTransport(defaultConfig);
        await using var client = await McpClientFactory.CreateAsync(
         sseTransport,
         defaultOptions,
         loggerFactory: _loggerFactory);

        var tools = await client.ListToolsAsync();

        // 只使用需要的插件
        tools = tools.Where(x => pluginFunctionEntities.Any(y => y.Name == x.Name)).ToList();
        KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions(
            pluginName: pluginEntity.PluginName,
            description: pluginEntity.Description ?? string.Empty,
            functions: tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        return plugin;
    }
}
