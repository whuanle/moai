using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using System.Runtime.CompilerServices;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// 聊天生成.
/// </summary>
public class ChatCompletionsCommandHandler : IStreamRequestHandler<ChatCompletionsCommand, IOpenAIChatCompletionsObject>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCompletionsCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="aiClientBuilder"></param>
    public ChatCompletionsCommandHandler(IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder)
    {
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IOpenAIChatCompletionsObject> Handle(ChatCompletionsCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string completionId = $"chatcmpl-" + Guid.CreateVersion7().ToString("N");

        int funcIteraCount = 1;

        // 拼接 ai 回复的所有内容
        StringBuilder responseContent = new StringBuilder();
        OpenAIChatCompletionsObject openAIChatCompletionsObject = default!;

        List<OpenAI.Chat.ChatTokenUsage> useages = new List<OpenAI.Chat.ChatTokenUsage>();

        var kernelBuilder = Kernel.CreateBuilder();
        foreach (var plugin in request.Plugins)
        {
            kernelBuilder.Plugins.Add(plugin);
        }

        var kernel = _aiClientBuilder.Configure(kernelBuilder, request.Endpoint)
            .Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            ModelId = request.Endpoint.Name,

            // 手动执行函数
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false),
        };

        if (executionSettings.ExtensionData == null)
        {
            executionSettings.ExtensionData = new Dictionary<string, object>();
        }

        // 添加影响因素
        foreach (var item in request.ExecutionSetting)
        {
            executionSettings.ExtensionData.Add(item.Key, item.Value);
        }

        // 采用手动执行函数的方式，所以需要套一层循环
        while (true || !cancellationToken.IsCancellationRequested)
        {
            // 当前对话
            var responseStream = chatCompletionService.GetStreamingChatMessageContentsAsync(
                chatHistory: request.ChatHistory,
                kernel: kernel,
                executionSettings: executionSettings,
                cancellationToken: cancellationToken);

            // 检测函数调用
            var fccBuilder = new FunctionCallContentBuilder();

            // 当前对话
            await foreach (var chunk in responseStream)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                fccBuilder.Append(chunk);

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

                // stop 不表示已经结束，后续还会返回 tokens 使用量等信息
                if (streamingChatCompletionUpdate.FinishReason != null && streamingChatCompletionUpdate.FinishReason == OpenAI.Chat.ChatFinishReason.Stop)
                {
                    openAIChatCompletionsObject = new OpenAIChatCompletionsObject
                    {
                        Model = request.Endpoint.Name,
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        SystemFingerprint = streamingChatCompletionUpdate.SystemFingerprint,
                        Id = completionId,
                        Choices = new List<OpenAIChatCompletionsChoice>
                        {
                            new OpenAIChatCompletionsChoice
                            {
                                Index = chunk.ChoiceIndex,
                                FinishReason = Microsoft.Extensions.AI.ChatFinishReason.Stop.ToString(),
                                Message = new OpenAIChatCompletionsDelta
                                {
                                    Role = (chunk.Role ?? AuthorRole.Assistant).Label,
                                    Content = responseContent.ToString(),
                                }
                            }
                        },
                    };
                    continue;
                }

                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    responseContent.Append(chunk.Content);
                }

                yield return new OpenAIChatCompletionsChunk
                {
                    Model = request.Endpoint.Name,
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    SystemFingerprint = streamingChatCompletionUpdate.SystemFingerprint,
                    Id = completionId,
                    Choices = new List<OpenAIChatCompletionsChoice>
                    {
                        new OpenAIChatCompletionsChoice
                        {
                            Index = chunk.ChoiceIndex,
                            FinishReason = null,
                            Delta = new OpenAIChatCompletionsDelta
                            {
                                Content = chunk.Content ?? string.Empty,
                                Role = chunk.Role?.Label ?? "assistant"
                            }
                        }
                    },
                };
            }

            // 如果当前对话不需要执行任何函数，说明已经真正结束对话
            var functionCalls = fccBuilder.Build();
            if (!functionCalls.Any())
            {
                break;
            }

            // 因为手动调用函数会导致中间缺失 AI 响应需要调用的函数列表，需要手动加上去
            ChatMessageContent? fcContent = new ChatMessageContent(role: AuthorRole.Assistant, content: null);
            request.ChatHistory.Add(fcContent);

            // 执行函数调用
            // 流式返回时，前端根据 FinishReason 区分是否函数调用，根据 是否有 Content 判断是调用请求还是调用结果
            int functionIndex = 0;
            foreach (var functionCall in functionCalls)
            {
                funcIteraCount++;
                string systemFingerprint = $"fp_{funcIteraCount.ToString("x2")}";

                // 手动模拟 AI 要调用的函数
                fcContent.Items.Add(functionCall);

                // 准备调用函数，目前已知函数参数
                yield return new OpenAIChatCompletionsChunk
                {
                    Model = request.Endpoint.Name,
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

                    SystemFingerprint = systemFingerprint,
                    Id = completionId,
                    Choices = new List<OpenAIChatCompletionsChoice>
                    {
                        new OpenAIChatCompletionsChoice
                        {
                            Index = functionIndex,
                            FinishReason = Microsoft.Extensions.AI.ChatFinishReason.ToolCalls.ToString(),
                            Delta = new OpenAIChatCompletionsDelta
                            {
                                Role = AuthorRole.Tool.Label,
                                ToolCalls = new List<OpenAiToolCall>
                                {
                                    new OpenAiToolCall
                                    {
                                        Id = functionCall!.Id!,
                                        Type = "function",
                                        Function = new OpenAiFunctionCall
                                        {
                                            Name = functionCall.PluginName + "-" + functionCall.FunctionName,
                                            Arguments = functionCall!.Arguments!
                                        }
                                    }
                                },
                            }
                        }
                    },
                };

                var functionResult = await functionCall.InvokeAsync(kernel);
                request.ChatHistory.Add(functionResult.ToChatMessage());

                // 函数调用结果
                yield return new OpenAIChatCompletionsChunk
                {
                    Model = request.Endpoint.Name,
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    SystemFingerprint = systemFingerprint,
                    Id = completionId,
                    Choices = new List<OpenAIChatCompletionsChoice>
                    {
                        new OpenAIChatCompletionsChoice
                        {
                            Index = functionIndex,
                            FinishReason = Microsoft.Extensions.AI.ChatFinishReason.ToolCalls.ToString(),
                            Delta = new OpenAIChatCompletionsDelta
                            {
                                Role = AuthorRole.Tool.Label,
                                Content = functionResult.Result.ToJsonString()
                            }
                        }
                    },
                };

                functionIndex++;
            }
        }

        openAIChatCompletionsObject = new OpenAIChatCompletionsObject
        {
            Model = openAIChatCompletionsObject.Model,
            Created = openAIChatCompletionsObject.Created,
            SystemFingerprint = openAIChatCompletionsObject.SystemFingerprint,
            Choices = new List<OpenAIChatCompletionsChoice>
            {
                new OpenAIChatCompletionsChoice
                {
                    Index = 0,
                    FinishReason = Microsoft.Extensions.AI.ChatFinishReason.Stop.ToString(),
                    Message = new OpenAIChatCompletionsDelta
                    {
                        Role = (request.ChatHistory.LastOrDefault()?.Role ?? AuthorRole.Assistant).Label,
                        Content = responseContent.ToString(),
                    }
                },
            },
            Id = openAIChatCompletionsObject.Id,
            Usage = new OpenAIChatCompletionsUsage
            {
                PromptTokens = useages.Sum(x => x.InputTokenCount),
                CompletionTokens = useages.Sum(x => x.OutputTokenCount),
                TotalTokens = useages.Sum(x => x.TotalTokenCount)
            }
        };

        yield return openAIChatCompletionsObject;
    }
}
