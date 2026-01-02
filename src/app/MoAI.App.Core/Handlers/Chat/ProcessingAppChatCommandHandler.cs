#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Events;
using MoAI.App.AIAssistant.Handlers;
using MoAI.App.Commands.Chat;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using Org.BouncyCastle.Utilities.Collections;
using System.Runtime.CompilerServices;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="ProcessingAppChatCommand"/>
/// </summary>
public partial class ProcessingAppChatCommandHandler : IStreamRequestHandler<ProcessingAppChatCommand, AiProcessingChatItem>, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INativePluginFactory _nativePluginFactory;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly ILogger<ProcessingAppChatCommandHandler> _logger;
    private readonly IMessagePublisher _messagePublisher;

    private readonly List<IDisposable> _disposables = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingAppChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="messagePublisher"></param>
    public ProcessingAppChatCommandHandler(
        IServiceProvider serviceProvider,
        DatabaseContext databaseContext,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        INativePluginFactory nativePluginFactory,
        IAiClientBuilder aiClientBuilder,
        IMessagePublisher messagePublisher)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _mediator = mediator;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _nativePluginFactory = nativePluginFactory;
        _aiClientBuilder = aiClientBuilder;
        _logger = loggerFactory.CreateLogger<ProcessingAppChatCommandHandler>();
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AiProcessingChatItem> Handle(ProcessingAppChatCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 获取应用信息
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var appCommonEntity = await _databaseContext.AppCommons
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appCommonEntity == null)
        {
            throw new BusinessException("应用配置不存在") { StatusCode = 404 };
        }

        // 创建新对话
        var chatId = request.ChatId;
        if (request.ChatId != null && request.ChatId.Value != default)
        {
            var chatEntity = await _databaseContext.AppCommonChats
                .Where(x => x.Id == request.ChatId && x.AppId == request.AppId && x.TeamId == request.TeamId)
                .FirstOrDefaultAsync(cancellationToken);

            if (chatEntity == null)
            {
                throw new BusinessException("对话不存在") { StatusCode = 404 };
            }
        }
        else
        {
            var chatEntity = await _databaseContext.AppCommonChats
                .AddAsync(
                new AppCommonChatEntity
                {
                    AppId = request.AppId,
                    TeamId = request.TeamId,
                    Title = "新对话",
                    UserType = (int)request.ContextUserType,
                    CreateUserId = request.ContextUserId,
                    UpdateUserId = request.ContextUserId,
                },
                cancellationToken);
        }

        // 补全对话上下文，无论是否第一次新建，后续按流程走就行
        var history = await _databaseContext.AppCommonChatHistories
            .Where(x => x.ChatId == chatId)
            .OrderBy(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        string completionId = $"chatcmpl-" + Guid.CreateVersion7().ToString("N");

        var aiEndpoint = await _databaseContext.AiModels
            .Where(x => x.Id == appCommonEntity.ModelId)
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

        var pluginKeys = appCommonEntity.Plugins.JsonToObject<IReadOnlyCollection<string>>() ?? Array.Empty<string>();
        var wikiIds = appCommonEntity.WikiIds.JsonToObject<IReadOnlyCollection<int>>() ?? Array.Empty<int>();

        List<OpenAIChatCompletionsUsage> useages = new List<OpenAIChatCompletionsUsage>();
        ProcessingAiAssistantChatContext chatContext = new()
        {
            ChatId = request.AppId,
            AiModel = aiEndpoint,
        };

        var plugins = await GetPluginsAsync(chatContext, pluginKeys, wikiIds);

        // 构建对话历史
        ChatHistory chatMessages = RestoreChatHistory(history, appCommonEntity.Prompt);

        var userQuestion = await GenerateQuestion(request, chatMessages);

        var userChatRecord = new AppCommonChatHistoryEntity
        {
            ChatId = chatId!.Value,
            CompletionsId = completionId,
            Content = userQuestion.ToJsonString(),
            Role = AuthorRole.User.Label,
        };

        await _databaseContext.AppCommonChatHistories.AddAsync(userChatRecord);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var kernelBuilder = Kernel.CreateBuilder();
        foreach (var plugin in plugins)
        {
            kernelBuilder.Plugins.Add(plugin);
        }

        kernelBuilder.Services.AddSingleton<IFunctionInvocationFilter>(new PluginFunctionInvocationFilter(chatContext));
        var kernel = _aiClientBuilder.Configure(kernelBuilder, aiEndpoint)
            .Build();

        var chatCompletionService = kernel.Services.GetRequiredKeyedService<IChatCompletionService>("MoAI");

        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            ModelId = aiEndpoint.Name,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        if (executionSettings.ExtensionData == null)
        {
            executionSettings.ExtensionData = new Dictionary<string, object>();
        }

        // 添加影响因素
        var settings = appCommonEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>() ?? Array.Empty<KeyValueString>();
        foreach (var item in settings)
        {
            executionSettings.ExtensionData.Add(item.Key, item.Value);
        }

        // 当前对话
        var responseStream = chatCompletionService.GetStreamingChatMessageContentsAsync(
            chatHistory: chatMessages,
            kernel: kernel,
            executionSettings: executionSettings,
            cancellationToken: cancellationToken);

        var unifyedStream = new UnifyAiResponseStreamCommand
        {
            ResponseStream = responseStream,
            ChatContext = chatContext
        };

        // 统一对话处理
        AiProcessingChatItem lastChunk = default!;
        bool isException = false;
        var unifyStream = _mediator.CreateStream(unifyedStream, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var hasNext = await unifyStream.MoveNextAsync();
                if (!hasNext)
                {
                    break;
                }

                lastChunk = unifyStream.Current;
                if (lastChunk.Usage != null)
                {
                    useages.Add(lastChunk.Usage);
                }
            }
            catch (Exception ex)
            {
                isException = true;
                _logger.LogError(ex, "Error occurred while processing the conversation stream.");
                break;
            }

            // 外部应用不可以显示调用的插件内容
            if (appEntity.IsForeign)
            {
                var newChoices = lastChunk.Choices.Where(choice => choice.StreamType != AiProcessingChatStreamType.Plugin).ToArray();
                if (newChoices != null)
                {
                    yield return new AiProcessingChatItem
                    {
                        Choices = newChoices,
                        FinishReason = lastChunk.FinishReason,
                        Usage = lastChunk.Usage
                    };
                }

                yield return lastChunk;
            }

            // 可能因为报错或取消导致流式提前结束
            if (lastChunk == null || string.IsNullOrEmpty(lastChunk.FinishReason))
            {
                var choices = new List<AiProcessingChoice>
            {
                new AiProcessingChoice
                {
                    StreamType = AiProcessingChatStreamType.Text,
                    StreamState = isException ? AiProcessingChatStreamState.Error : AiProcessingChatStreamState.End,
                    TextCall = new DefaultAiProcessingTextCall
                    {
                        Content = "对话被取消.",
                    }
                }
            };

                if (isException)
                {
                    yield return new AiProcessingChatItem
                    {
                        FinishReason = "error",
                        Choices = choices
                    };
                }
                else
                {
                    yield return new AiProcessingChatItem
                    {
                        FinishReason = "stop",
                        Choices = choices
                    };
                }
            }

            var usage = new OpenAIChatCompletionsUsage
            {
                PromptTokens = useages.Sum(x => x.PromptTokens),
                CompletionTokens = useages.Sum(x => x.CompletionTokens),
                TotalTokens = useages.Sum(x => x.TotalTokens)
            };

            await _messagePublisher.AutoPublishAsync(
                new AiModelUseageMessage
                {
                    AiModelId = appCommonEntity.ModelId,
                    Channel = "app_debug",
                    ContextUserId = request.ContextUserId,
                    Usage = usage
                });
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var item in _disposables)
        {
            item.Dispose();
        }

        foreach (var item in _asyncDisposables)
        {
            await item.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
