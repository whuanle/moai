#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CA1051 // 不要声明可见实例字段
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表

using Maomi.MQ;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MoAI.AI.Chat.Models;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Events;
using MoAI.AiModel.Queries;
using MoAI.App.AIAssistant.Handlers;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Plugin;
using System.Runtime.CompilerServices;

namespace MoAI.AI.Chat;

/// <summary>
/// 统一流式对话抽象.
/// </summary>
public abstract partial class ProcessingChatStreamBase : IAsyncDisposable
{
    protected readonly IServiceProvider _serviceProvider;
    protected readonly DatabaseContext _databaseContext;
    protected readonly IMediator _mediator;
    protected readonly ILoggerFactory _loggerFactory;
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly INativePluginFactory _nativePluginFactory;
    protected readonly IAiClientBuilder _aiClientBuilder;
    protected readonly ILogger<ProcessingChatStreamBase> _logger;
    protected readonly IMessagePublisher _messagePublisher;

    protected readonly List<IDisposable> _disposables = new();
    protected readonly List<IAsyncDisposable> _asyncDisposables = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingChatStreamBase"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="messagePublisher"></param>
    protected ProcessingChatStreamBase(
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
        _logger = loggerFactory.CreateLogger<ProcessingChatStreamBase>();
        _messagePublisher = messagePublisher;
    }

    /// <summary>
    /// 流式对话.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async IAsyncEnumerable<AiProcessingChatItem> HandlerAsync(ProcessingChatStreamParameters request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string completionId = request.CompletionId;

        var aiEndpoint = await _mediator.Send(new QueryAiModelToAiEndpointCommand { AiModelId = request.ModelId });

        if (aiEndpoint == null || aiEndpoint.AiModelType != AiModelType.Chat)
        {
            throw new BusinessException("未找到模型") { StatusCode = 404 };
        }

        Dictionary<string, string> pluginKeyNames = new();
        ProcessingAiAssistantChatContext chatContext = new()
        {
            ChatId = request.ChatId,
            AiModel = aiEndpoint,
            PluginKeyNames = pluginKeyNames,
        };

        // 构建插件
        var plugins = await GetPluginsAsync(chatContext, pluginKeyNames, request.Plugins, request.WikiIds);

        // 生成对话上下文
        ChatHistory chatMessages = RestoreChatHistory(request.ChatMessages, request.Prompt);
        var userQuestion = await GenerateChatMessageContent(request.ChatId, request.Question, request.FileKey);
        chatMessages.AddUserMessage(userQuestion);

        // 构建客户端
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
        foreach (var item in request.ExecutionSettings)
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
            }
            catch (Exception ex)
            {
                isException = true;
                _logger.LogError(ex, "Error occurred while processing the conversation stream.");
                break;
            }

            yield return lastChunk;
        }

        var usage = new OpenAIChatCompletionsUsage
        {
            PromptTokens = chatContext.Usage.Sum(x => x.PromptTokens),
            CompletionTokens = chatContext.Usage.Sum(x => x.CompletionTokens),
            TotalTokens = chatContext.Usage.Sum(x => x.TotalTokens)
        };

        // 即使是提前结束，UnifyAiResponseStreamCommand 也会返回最后一块数据，这里只处理没有返回结束标记的情况，大概率是异常了
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

            var finishReason = "stop";
            if (isException)
            {
                finishReason = "error";
            }

            lastChunk = new AiProcessingChatItem
            {
                FinishReason = finishReason,
                Usage = usage,
                Choices = chatContext.Choices.Select(x =>
                {
                    if (x.TextCall != null)
                    {
                        x.TextCall.Refresh();
                    }

                    return x.ToAiProcessingChoice();
                }).ToList()
            };

            yield return lastChunk;
        }

        // 模型用量统计
        await _messagePublisher.AutoPublishAsync(
            new AiModelUseageMessage
            {
                AiModelId = request.ModelId,
                Channel = "chat",
                ContextUserId = request.ContextUserId,
                TokenUsage = usage!,
                PluginUsage = chatContext.Choices.Where(x => x.PluginCall != null).GroupBy(x => x.PluginCall!.PluginKey).ToDictionary(x => x.Key, x => x.Count())
            });
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
