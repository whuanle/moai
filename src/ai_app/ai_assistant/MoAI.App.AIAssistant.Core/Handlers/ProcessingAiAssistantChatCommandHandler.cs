#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using System.Runtime.CompilerServices;
using System.Text;

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
    public ProcessingAiAssistantChatCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, IMediator mediator, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, INativePluginFactory nativePluginFactory, IAiClientBuilder aiClientBuilder)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _mediator = mediator;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _nativePluginFactory = nativePluginFactory;
        _aiClientBuilder = aiClientBuilder;
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

        var userChatRecord = new AppAssistantChatHistoryEntity
        {
            ChatId = chatObjectEntity.Id,
            CompletionsId = completionId,
            Content = new List<DefaultAiProcessingChoice>
            {
                new DefaultAiProcessingChoice
                {
                    StreamType = AiProcessingChatStreamType.Text,
                    StreamState = AiProcessingChatStreamState.End,
                    TextCall = new DefaultAiProcessingTextCall
                    {
                        Content = request.Content
                    }
                }
            }.ToJsonString(),
            Role = AuthorRole.User.Label,
        };

        await _databaseContext.AppAssistantChatHistories.AddAsync(userChatRecord);
        await _databaseContext.SaveChangesAsync(cancellationToken);

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

        List<OpenAI.Chat.ChatTokenUsage> useages = new List<OpenAI.Chat.ChatTokenUsage>();
        ProcessingAiAssistantChatContext chatContext = new()
        {
            ChatId = chatObjectEntity.Id,
            AiModel = aiEndpoint,
        };

        var plugins = await GetPluginsAsync(chatContext, pluginKeys, wikiId);

        ChatHistory chatMessages = GeneraiChatMessage(history, chatObjectEntity.Prompt);

        chatMessages.AddUserMessage(request.Content);

        var kernelBuilder = Kernel.CreateBuilder();
        foreach (var plugin in plugins)
        {
            kernelBuilder.Plugins.Add(plugin);
        }

        kernelBuilder.Services.AddSingleton<IFunctionInvocationFilter>(new PluginFunctionInvocationFilter(chatContext));
        var kernel = _aiClientBuilder.Configure(kernelBuilder, aiEndpoint)
            .Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

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

        await foreach (var chunk in responseStream)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // todo: 兼容 openai 的接口，openai、azure、deepseek，其它 google 的接口待测试
            var streamingChatCompletionUpdate = chunk.InnerContent as OpenAI.Chat.StreamingChatCompletionUpdate;
            if (streamingChatCompletionUpdate == null)
            {
                continue;
            }

            if (streamingChatCompletionUpdate.Usage != null)
            {
                useages.Add(streamingChatCompletionUpdate.Usage);
            }

            // 先推送最近一次的 choice
            var lastChoice = chatContext.Choices.LastOrDefault();
            if (lastChoice != null && lastChoice.IsPush == false)
            {
                lastChoice.IsPush = true;
                yield return new AiProcessingChatItem
                {
                    Choices = new List<AiProcessingChoice>
                    {
                        lastChoice.ToAiProcessingChoice()
                    }
                };
            }

            // 包括了各种类型的对象
            var contentUpdate = streamingChatCompletionUpdate.ContentUpdate;
            if (contentUpdate != null && contentUpdate.Count > 0)
            {
                // 先结束上一个块
                if (lastChoice != null && lastChoice.StreamType != AiProcessingChatStreamType.Text)
                {
                    await foreach (var end in EndChoiceAsync(AiProcessingChatStreamType.Text, lastChoice))
                    {
                        yield return end;
                    }
                }

                if (lastChoice == null || lastChoice.StreamType != AiProcessingChatStreamType.Text || lastChoice.StreamState == AiProcessingChatStreamState.Error || lastChoice.StreamState == AiProcessingChatStreamState.End)
                {
                    lastChoice = new DefaultAiProcessingChoice
                    {
                        StreamType = AiProcessingChatStreamType.Text,
                        StreamState = AiProcessingChatStreamState.Start,
                        TextCall = new DefaultAiProcessingTextCall
                        {
                            Content = chunk.Content!,
                            ContentBuilder = new StringBuilder()
                        }
                    };

                    lastChoice.TextCall.ContentBuilder.Append(chunk.Content);
                    chatContext.Choices.Add(lastChoice);

                    lastChoice.IsPush = true;
                    yield return new AiProcessingChatItem
                    {
                        Choices = new List<AiProcessingChoice>
                        {
                            lastChoice.ToAiProcessingChoice()
                        }
                    };
                }
                else
                {
                    lastChoice.TextCall!.ContentBuilder.Append(chunk.Content);
                    lastChoice.StreamState = AiProcessingChatStreamState.Processing;
                    lastChoice.IsPush = true;

                    yield return new AiProcessingChatItem
                    {
                        Choices = new List<AiProcessingChoice>
                        {
                            new AiProcessingChoice
                            {
                                StreamType = AiProcessingChatStreamType.Text,
                                StreamState = AiProcessingChatStreamState.Processing,
                                TextCall = new DefaultAiProcessingTextCall
                                {
                                    Content = chunk.Content!
                                }
                            }
                        }
                    };
                }
            }

            // 函数调用
            var toolCallUpdates = streamingChatCompletionUpdate.ToolCallUpdates;
            foreach (var item in toolCallUpdates)
            {
                if (item.Kind != OpenAI.Chat.ChatToolCallKind.Function)
                {
                    continue;
                }

                // 先结束上一个块
                if (lastChoice != null && lastChoice.StreamType != AiProcessingChatStreamType.Plugin)
                {
                    await foreach (var end in EndChoiceAsync(AiProcessingChatStreamType.Plugin, lastChoice))
                    {
                        yield return end;
                    }
                }

                // 上一个不是插件，或者上一个插件已经结束
                if (lastChoice == null || lastChoice.StreamType != AiProcessingChatStreamType.Plugin || lastChoice.StreamState == AiProcessingChatStreamState.Error || lastChoice.StreamState == AiProcessingChatStreamState.End)
                {
                    // 还没有走到调用那一步
                    if (string.IsNullOrEmpty(item.FunctionName))
                    {
                        continue;
                    }

                    var pluginType = item.FunctionName.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)
                        ? PluginType.WikiPlugin
                        : PluginType.NativePlugin;

                    string pluginKey = string.Empty;
                    if (pluginType == PluginType.WikiPlugin)
                    {
                        // wiki_6-invoke
                        pluginKey = item.FunctionName.Split('-').FirstOrDefault()!;
                    }
                    else
                    {
                        pluginKey = item.FunctionName.Split('-').FirstOrDefault()!;
                    }

                    // 开始调用函数
                    // todo: 异常？请求参数？
                    lastChoice = new DefaultAiProcessingChoice
                    {
                        StreamType = AiProcessingChatStreamType.Plugin,
                        StreamState = AiProcessingChatStreamState.Start,
                        PluginCall = new DefaultAiProcessingPluginCall
                        {
                            ToolCallId = item.ToolCallId,
                            PluginKey = pluginKey,
                            PluginName = chatContext.PluginKeyNames.FirstOrDefault(x => x.Key == pluginKey).Value ?? string.Empty,
                            FunctionName = item.FunctionName,
                            PluginType = pluginType,
                            Result = string.Empty
                        }
                    };

                    chatContext.Choices.Add(lastChoice);
                    lastChoice.IsPush = true;
                    yield return new AiProcessingChatItem
                    {
                        Choices = new List<AiProcessingChoice> { lastChoice.ToAiProcessingChoice() }
                    };
                }
                else
                {
                    if (lastChoice.StreamState != AiProcessingChatStreamState.End && lastChoice.StreamState != AiProcessingChatStreamState.Error)
                    {
                        lastChoice.IsPush = true;
                        lastChoice.StreamState = AiProcessingChatStreamState.Processing;
                        yield return new AiProcessingChatItem
                        {
                            Choices = new List<AiProcessingChoice> { lastChoice.ToAiProcessingChoice() }
                        };
                    }
                }
            }

            if (streamingChatCompletionUpdate.Usage != null)
            {
                useages.Add(streamingChatCompletionUpdate.Usage);
            }

            // 返回的是音频
            var audioUpdate = streamingChatCompletionUpdate.OutputAudioUpdate;
            if (audioUpdate != null)
            {
            }

            // 拒绝理由
            var refusalUpdate = streamingChatCompletionUpdate.RefusalUpdate;
            if (refusalUpdate != null)
            {
            }
        }

        // 最后一个块结束
        var finishChoice = chatContext.Choices.LastOrDefault();
        if (finishChoice != null && finishChoice.StreamState != AiProcessingChatStreamState.End && finishChoice.StreamState != AiProcessingChatStreamState.Error)
        {
            finishChoice.StreamState = AiProcessingChatStreamState.End;

            finishChoice.IsPush = true;
            yield return new AiProcessingChatItem
            {
                Choices = new List<AiProcessingChoice>
                    {
                        finishChoice.ToAiProcessingChoice()
                    }
            };
        }

        var usage = new OpenAIChatCompletionsUsage
        {
            PromptTokens = useages.Sum(x => x.InputTokenCount),
            CompletionTokens = useages.Sum(x => x.OutputTokenCount),
            TotalTokens = useages.Sum(x => x.TotalTokenCount)
        };

        // 要考虑中途取消了

        // 生成汇总
        yield return new AiProcessingChatItem
        {
            FinishReason = "stop",
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

        chatObjectEntity.OutTokens += usage.CompletionTokens;
        chatObjectEntity.InputTokens += usage.PromptTokens;
        chatObjectEntity.TotalTokens += usage.TotalTokens;

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

        //var log = new AiModelUseageLogEntity
        //{
        //    Channel = "assistant",
        //    CompletionTokens = completionsObject.Usage.CompletionTokens,
        //    ModelId = chatObjectEntity.ModelId,
        //    PromptTokens = completionsObject.Usage.PromptTokens,
        //    TotalTokens = completionsObject.Usage.TotalTokens,
        //    UseriId = request.ContextUserId
        //};

        //await _databaseContext.AiModelUseageLogs.AddAsync(log);
        //await _databaseContext.SaveChangesAsync();
    }

    private static ChatHistory GeneraiChatMessage(List<AppAssistantChatHistoryEntity> history, string? prompt)
    {
        ChatHistory chatMessages = new();

        // 添加提示词.
        if (!string.IsNullOrEmpty(prompt))
        {
            chatMessages.AddSystemMessage(prompt);
        }

        // todo: 后期优化
        foreach (var item in history)
        {
            var contents = item.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>();

            if (item.Role == AuthorRole.User.Label)
            {
                foreach (var content in contents)
                {
                    if (content.TextCall == null)
                    {
                        continue;
                    }

                    // 后期考虑图片
                    chatMessages.AddAssistantMessage(content: content.TextCall.Content);
                }
            }
            else if (item.Role == AuthorRole.Assistant.Label)
            {
                foreach (var content in contents)
                {
                    if (content.StreamType == AiProcessingChatStreamType.Text && content.TextCall != null)
                    {
                        chatMessages.AddAssistantMessage(content.TextCall.Content);
                    }

                    // todo： 要考虑失败的插件调用
                    else if (content.StreamType == AiProcessingChatStreamType.Plugin && content.PluginCall != null)
                    {
                        //var arguments = new KernelArguments();

                        //foreach (var argument in content.PluginCall.Params)
                        //{
                        //    arguments.Add(argument.Key, argument.Value);
                        //}

                        //var funcCallContent = new FunctionCallContent(
                        //    functionName: content.PluginCall.FunctionName,
                        //    pluginName: content.PluginCall.PluginName,
                        //    id: content.PluginCall.ToolCallId,
                        //    arguments: arguments)
                        //{
                        //};

                        //var funcResultContent = new FunctionResultContent(
                        //    functionName: content.PluginCall.FunctionName,
                        //    pluginName: content.PluginCall.PluginName,
                        //    callId: content.PluginCall.ToolCallId,
                        //    result: content.PluginCall.Result);

                        //ChatMessageContent chatMessageContent = new()
                        //{
                        //    Role = AuthorRole.Tool,
                        //    AuthorName = AuthorRole.Tool.Label,
                        //    Items = new ChatMessageContentItemCollection
                        //    {
                        //        funcCallContent,
                        //        funcResultContent
                        //    }
                        //};

                        //chatMessages.Add(chatMessageContent);
                        chatMessages.Add(new ChatMessageContent
                        {
                            Role = AuthorRole.System,
                            AuthorName = AuthorRole.System.Label,
                            Content = $"""
                            插件名称：{content.PluginCall.PluginName}
                            调用函数：{content.PluginCall.FunctionName}
                            参数：{string.Join(',', content.PluginCall.Params.Select(x => $"{x.Key}={x.Value}"))}
                            结果：{content.PluginCall.Result}
                            """
                        });
                    }
                }
            }
            else if (item.Role == AuthorRole.System.Label)
            {
                chatMessages.AddSystemMessage(item.Content);
            }
            else if (item.Role == AuthorRole.Tool.Label)
            {
                //chatMessages.Add(new ChatMessageContent
                //{
                //    MimeType
                //});
            }
            else
            {
                // 其他角色不处理
                continue;
            }
        }

        return chatMessages;
    }

    // 结束上一个块
    private async IAsyncEnumerable<AiProcessingChatItem> EndChoiceAsync(AiProcessingChatStreamType exclude, DefaultAiProcessingChoice choice)
    {
        await Task.CompletedTask;

        if (choice.IsPush)
        {
            yield break;
        }

        if (choice.StreamState == AiProcessingChatStreamState.Error || choice.StreamState == AiProcessingChatStreamState.End)
        {
            choice.IsPush = true;
            yield return new AiProcessingChatItem
            {
                Choices = new List<AiProcessingChoice>
                {
                    choice.ToAiProcessingChoice()
                }
            };

            yield break;
        }

        // 终结
        if (choice.StreamType != exclude)
        {
            choice.StreamState = AiProcessingChatStreamState.End;
            choice.IsPush = true;

            yield return new AiProcessingChatItem
            {
                Choices = new List<AiProcessingChoice>
                {
                    choice.ToAiProcessingChoice()
                }
            };
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