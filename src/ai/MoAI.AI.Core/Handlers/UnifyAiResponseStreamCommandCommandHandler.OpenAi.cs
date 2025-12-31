#pragma warning disable OPENAI001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using MoAI.AI.Models;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// OpenAI 流式处理支持.
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler
{
    private static async IAsyncEnumerable<AiProcessingChatItem> UseOpenAiStream(List<OpenAIChatCompletionsUsage> useages, ProcessingAiAssistantChatContext chatContext, Microsoft.SemanticKernel.StreamingChatMessageContent chunk)
    {
        // todo: 兼容 openai 的接口，openai、azure、deepseek，其它 google 的接口待测试
        var streamingChatCompletionUpdate = chunk.InnerContent as OpenAI.Chat.StreamingChatCompletionUpdate;

        if (streamingChatCompletionUpdate == null)
        {
            yield break;
        }

        if (streamingChatCompletionUpdate.Usage != null)
        {
            useages.Add(new OpenAIChatCompletionsUsage
            {
                CompletionTokens = streamingChatCompletionUpdate.Usage.OutputTokenCount,
                PromptTokens = streamingChatCompletionUpdate.Usage.InputTokenCount,
                TotalTokens = streamingChatCompletionUpdate.Usage.TotalTokenCount
            });
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

                // 去掉结尾的 _invoke 或 -invoke
                var pluginKey = item.FunctionName.EndsWith("_invoke", StringComparison.CurrentCultureIgnoreCase)
                    ? item.FunctionName[..^7]
                    : item.FunctionName.EndsWith("-invoke", StringComparison.CurrentCultureIgnoreCase)
                        ? item.FunctionName[..^7]
                        : item.FunctionName;

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
            useages.Add(new OpenAIChatCompletionsUsage
            {
                CompletionTokens = streamingChatCompletionUpdate.Usage.OutputTokenCount,
                PromptTokens = streamingChatCompletionUpdate.Usage.InputTokenCount,
                TotalTokens = streamingChatCompletionUpdate.Usage.TotalTokenCount
            });
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
}
