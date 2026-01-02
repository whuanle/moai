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
using MoAI.App.AIAssistant.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using System.Runtime.CompilerServices;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// <inheritdoc cref="ProcessingAiAssistantChatCommand"/>
/// </summary>
public partial class ProcessingAiAssistantChatCommandHandler : IStreamRequestHandler<ProcessingAiAssistantChatCommand, AiProcessingChatItem>, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INativePluginFactory _nativePluginFactory;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly ILogger<ProcessingAiAssistantChatCommandHandler> _logger;
    private readonly IMessagePublisher _messagePublisher;

    private readonly List<IDisposable> _disposables = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingAiAssistantChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="messagePublisher"></param>
    public ProcessingAiAssistantChatCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, IMediator mediator, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, INativePluginFactory nativePluginFactory, IAiClientBuilder aiClientBuilder, IMessagePublisher messagePublisher)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _mediator = mediator;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _nativePluginFactory = nativePluginFactory;
        _aiClientBuilder = aiClientBuilder;
        _logger = loggerFactory.CreateLogger<ProcessingAiAssistantChatCommandHandler>();
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AiProcessingChatItem> Handle(ProcessingAiAssistantChatCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chatObjectEntity = await _databaseContext.AppAssistantChats
            .Where(x => x.Id == request.ChatId && x.CreateUserId == request.ContextUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatObjectEntity == null)
        {
            throw new BusinessException("对话不存在或无权访问") { StatusCode = 404 };
        }

        // 补全对话上下文
        var history = await _databaseContext.AppAssistantChatHistories
            .Where(x => x.ChatId == request.ChatId)
            .OrderBy(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        string completionId = $"chatcmpl-" + Guid.CreateVersion7().ToString("N");

        var aiEndpoint = await _databaseContext.AiModels
            .Where(x => x.Id == chatObjectEntity.ModelId)
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

        var pluginKeys = chatObjectEntity.Plugins.JsonToObject<IReadOnlyCollection<string>>()!;
        var wikiId = chatObjectEntity.WikiIds.JsonToObject<IReadOnlyCollection<int>>()!;

        List<OpenAIChatCompletionsUsage> useages = new List<OpenAIChatCompletionsUsage>();
        ProcessingAiAssistantChatContext chatContext = new()
        {
            ChatId = chatObjectEntity.Id,
            AiModel = aiEndpoint,
        };

        var plugins = await GetPluginsAsync(chatContext, pluginKeys, wikiId);

        ChatHistory chatMessages = RestoreChatHistory(history, chatObjectEntity.Prompt);

        var userQuestion = await GenerateQuestion(request, chatMessages);

        var userChatRecord = new AppAssistantChatHistoryEntity
        {
            ChatId = chatObjectEntity.Id,
            CompletionsId = completionId,
            Content = userQuestion.ToJsonString(),
            Role = AuthorRole.User.Label,
        };

        await _databaseContext.AppAssistantChatHistories.AddAsync(userChatRecord);
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

            // 自动执行函数
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        if (executionSettings.ExtensionData == null)
        {
            executionSettings.ExtensionData = new Dictionary<string, object>();
        }

        // 添加影响因素
        foreach (var item in chatObjectEntity.ExecutionSettings.JsonToObject<IReadOnlyCollection<KeyValueString>>()!)
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

        // 统一对话处理，适配各家的模型
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
                AiModelId = chatObjectEntity.ModelId,
                Channel = "chat",
                ContextUserId = request.ContextUserId,
                Usage = usage
            });

        chatObjectEntity.OutTokens += usage?.CompletionTokens ?? 0;
        chatObjectEntity.InputTokens += usage?.PromptTokens ?? 0;
        chatObjectEntity.TotalTokens += usage?.TotalTokens ?? 0;

        _databaseContext.Update(chatObjectEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var aiChatRecord = new AppAssistantChatHistoryEntity
        {
            ChatId = chatObjectEntity.Id,
            CompletionsId = completionId,
            Content = chatContext.Choices.ToJsonString(),
            Role = AuthorRole.Assistant.Label
        };

        await _databaseContext.AppAssistantChatHistories.AddAsync(aiChatRecord);
        await _databaseContext.SaveChangesAsync();
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