// <copyright file="ProcessingAiAssistantChatCommandHandler.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Helpers;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
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

    public async IAsyncEnumerable<IOpenAIChatCompletionsObject> Handle(ProcessingAiAssistantChatCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!ChatIdHelper.TryParseId(request.ChatId, out var chatId))
        {
            throw new BusinessException("对话记录不存在");
        }

        var chatEntity = await _databaseContext.ChatHistories.FirstOrDefaultAsync(x => x.ChatId == chatId);

        if (chatEntity == null)
        {
            throw new BusinessException("对话不存在");
        }

        await UpdateChatHistoryEntityAsync(request, chatEntity);

        // 生成插件列表
        List<KernelPlugin> functions = new List<KernelPlugin>();

        var pluginIds = request.PluginIds.Where(x => x > 0).ToHashSet();

        if (pluginIds.Count > 0)
        {
            var fs = await BuildPluginsAsync(chatEntity, pluginIds, cancellationToken);
            functions.AddRange(fs);
        }

        var wikiMemoryPlugin = await GetWikiPluginAsync(chatEntity, cancellationToken);
        functions.Add(wikiMemoryPlugin);

        var aiEndpoint = await _databaseContext.TeamAiModels
            .Where(x => x.TeamId == chatEntity.TeamId && x.Id == chatEntity.ModelId)
            .Select(x => new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                DisplayName = x.DisplayName,
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
                    Vision = x.Vision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (aiEndpoint == null)
        {
            throw new BusinessException("团队模型配置错误") { StatusCode = 500 };
        }

        var command = new ChatCompletionsCommand
        {
            Id = request.Id,
            ChatHistory = request.ChatHistory,
            Plugins = functions,
            Endpoint = aiEndpoint,
            ExecutionSetting = request.ExecutionSetting
        };

        await foreach (var item in _mediator.CreateStream(command, cancellationToken))
        {
            if (item is OpenAIChatCompletionsChunk chunk)
            {
                yield return System.Text.Json.JsonSerializer.Serialize(chunk);
            }
            else if (item is OpenAIChatCompletions chatObject)
            {
                // 最后结束的时候传输
                // todo: 需要统计 token 数量和流量等
                yield return System.Text.Json.JsonSerializer.Serialize(chatObject);
            }
            else
            {
                yield return item.ToString() ?? string.Empty;
            }
        }
    }

    private async Task UpdateChatHistoryEntityAsync(ProcessingAiAssistantChatCommand request, ChatHistoryEntity chatEntity)
    {
        chatEntity.Title = request.Title;
        chatEntity.ExecutionSettings = request.ExecutionSettings.ToJsonString();
        chatEntity.PluginIds = request.PluginIds.ToJsonString();
        chatEntity.ModelId = request.ModelId;
        chatEntity.WikiId = request.WikiId ?? 0;

        _databaseContext.Update(chatEntity);
        await _databaseContext.SaveChangesAsync();
    }

    private async Task<KernelPlugin> GetWikiPluginAsync(UserChatEntity chatEntity, CancellationToken cancellationToken)
    {
        // 读取知识库
        // todo: 后续是否支持多知识库

        var wikiEntity = await _databaseContext.TeamWikis
            .Where(x => x.TeamId == chatEntity.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        if (wikiEntity == null)
        {
            throw new BusinessException("知识库不存在或无权访问");
        }

        // 获取知识库配置信息
        var wikiConfigEntity = await _databaseContext.TeamWikiConfigs.FirstOrDefaultAsync(x => x.WikiId == wikiEntity.Id, cancellationToken);
        if (wikiConfigEntity == null)
        {
            throw new BusinessException("知识库系统配置错误");
        }

        // 该知识库使用的模型
        // todo： 后续判断知识库是否公开等
        var wikiAiModel = await _databaseContext.TeamAiModels
            .Where(x => x.Id == wikiConfigEntity.EmbeddingModelId)
            .FirstOrDefaultAsync(cancellationToken);
        if (wikiAiModel == null)
        {
            throw new BusinessException("知识库模型配置错误，无法使用知识库");
        }

        // 如果用户所在团队没有跟该知识库相同的模型类型，则不能使用知识库
        var userTeamAiModel = await _databaseContext.TeamAiModels
            .Where(x => x.TeamId == chatEntity.TeamId && x.Name == wikiAiModel.Name)
            .FirstOrDefaultAsync(cancellationToken);

        // 该团队下无与知识库相同的模型，则无法使用知识库
        if (userTeamAiModel == null)
        {
            throw new BusinessException("团队下无与知识库相同的模型，无法使用知识库") { StatusCode = 403 };
        }

        var aiEndpoint = new AiEndpoint
        {
            Name = wikiAiModel.Name,
            DeploymentName = wikiAiModel.DeploymentName,
            DisplayName = wikiAiModel.DisplayName,
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
                Vision = wikiAiModel.Vision,
            },
            MaxDimension = wikiAiModel.MaxDimension,
            TextOutput = wikiAiModel.TextOutput
        };

        var wikiConfig = new WikiConfig
        {
            EmbeddingDimensions = wikiConfigEntity.EmbeddingDimensions,
            EmbeddingBatchSize = wikiConfigEntity.EmbeddingBatchSize,
            MaxRetries = wikiConfigEntity.MaxRetries,
            EmbeddingModelTokenizer = wikiConfigEntity.EmbeddingModelTokenizer,
        };

        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder()
            .WithSimpleFileStorage(Path.GetTempPath());

        _customKernelMemoryBuilder.ConfigEmbeddingModel(memoryBuilder, aiEndpoint, wikiConfig);

        var memoryClient = memoryBuilder.WithoutTextGenerator()
            .WithPostgresMemoryDb(new PostgresConfig
            {
                ConnectionString = _systemOptions.DocumentStore.Database,
            })
            .Build();

        var memoryPlugin = new MemoryPlugin(defaultIndex: "n" + wikiEntity.Id.ToString(), memoryClient: memoryClient, waitForIngestionToComplete: true);
        return KernelPluginFactory.CreateFromObject(target: memoryPlugin, "KnowledgeMemory");
    }

    private async Task<IReadOnlyCollection<KernelPlugin>> BuildPluginsAsync(ProcessingAiAssistantChatCommand request, HashSet<int> pluginIds, CancellationToken cancellationToken)
    {
        List<KernelPlugin> functions = new();

        var pluginEntities = await _databaseContext.Plugins.Where(x => pluginIds.Contains(x.Id) && (x.CreateUserId == request.UserId || x.IsPublic)).ToListAsync(cancellationToken);

        var pluginGroupIds = pluginEntities.Select(x => x.GroupId).ToHashSet();

        var pluginGroupEntities = await _databaseContext.TeamPluginGroups
            .Where(x => x.TeamId == chatEntity.TeamId && pluginGroupIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        Dictionary<TeamPluginGroupEntity, List<TeamPluginEntity>> pluginLists = new Dictionary<TeamPluginGroupEntity, List<TeamPluginEntity>>();
        foreach (var group in pluginGroupEntities)
        {
            var plugins = pluginEntities.Where(x => x.GroupId == group.Id).ToList();
            if (plugins.Count > 0)
            {
                pluginLists.Add(group, plugins);
            }
        }

        // 翻译插件为 SK 支持的格式
        foreach (var item in pluginLists)
        {
            if (item.Key.Type == (int)PluginType.Mcp)
            {
                var plugin = await GetMCPPluginsAsync(item.Key, item.Value);
                functions.Add(plugin);
            }
            else if (item.Key.Type == (int)PluginType.OpenApi)
            {
                var plugin = await GetOpenApiPluginsAsync(item.Key, item.Value);
                functions.Add(plugin);
            }
            else if (item.Key.Type == (int)PluginType.System)
            {
                //// 系统插件
                //var systemFunctions = item.Value.Select(x => x.AsKernelFunction()).ToList();
                //functions.AddRange(systemFunctions);
            }
        }

        return functions;
    }

    private async Task<KernelPlugin> GetOpenApiPluginsAsync(PluginEntity pluginEntity, IReadOnlyCollection<PluginFunctionEntity> functionEntities)
    {
        // 后续抽到一个方法命令中
        IReadOnlyCollection<KeyValueString> headers = default!;
        try
        {
            headers = pluginEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>();
        }
        catch (Exception ex)
        {
            _ = ex;
            throw new BusinessException("Header 或 Query 格式不正确");
        }

        // 从 oss 读取 swaggger 文件，如果相同的 md5 可能本地有缓存，则直接读取缓存
        var fileEntity = await _databaseContext.Files
            .Where(x => x.Id == pluginEntity.OpenapiFileId)
            .FirstOrDefaultAsync();

        if (fileEntity == null)
        {
            throw new BusinessException("插件{0}已失效", pluginEntity.Name) { StatusCode = 429 };
        }

        var filePath = Path.Combine(Path.GetTempPath(), fileEntity.ObjectKey);
        if (!File.Exists(filePath))
        {
            await _mediator.Send(new DownloadFileCommand
            {
                ObjectKey = fileEntity.ObjectKey,
                StoreFilePath = filePath,
                Visibility = FileVisibility.Private
            });
        }

        OpenApiDocumentParser parser = new();
        using FileStream stream = File.OpenRead(filePath);
        RestApiSpecification specification = await parser.ParseAsync(stream);
        // var operations = specification.Operations.Where(x => pluginEntities.Any(p => p.Path == x.Path)).ToArray();

        var httpClient = _httpClientFactory.CreateClient("OpenApiClient");

        foreach (var header in headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        KernelPlugin plugin = OpenApiKernelPluginFactory.CreateFromOpenApi(
            pluginName: pluginEntity.Name,
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

    private async Task<KernelPlugin> GetMCPPluginsAsync(TeamPluginGroupEntity groupEntity, IReadOnlyCollection<TeamPluginEntity> pluginEntities)
    {
        // 后续抽到一个方法命令中
        Dictionary<string, string> headers = default!;
        Dictionary<string, string> queries = default!;
        try
        {
            headers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(groupEntity.Header);
            queries = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(groupEntity.Query);
        }
        catch (Exception ex)
        {
            _ = ex;
            throw new BusinessException("Header 或 Query 格式不正确");
        }

        // 第一步：创建 mcp 客户端
        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "MaomiAI", Version = "1.0.0" }
        };

        var uriBuilder = new UriBuilder(groupEntity.Server);
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
            Name = groupEntity.Name,
            AdditionalHeaders = headers ?? new Dictionary<string, string>(),
        };
        await using var sseTransport = new SseClientTransport(defaultConfig);
        await using var client = await McpClientFactory.CreateAsync(
         sseTransport,
         defaultOptions,
         loggerFactory: _loggerFactory);

        var tools = await client.ListToolsAsync();

        // 只使用需要的插件
        tools = tools.Where(x => pluginEntities.Any(y => y.Name == x.Name)).ToList();
        KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions(
            pluginName: groupEntity.Name,
            description: groupEntity.Description ?? string.Empty,
            functions: tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        return plugin;
    }
}
