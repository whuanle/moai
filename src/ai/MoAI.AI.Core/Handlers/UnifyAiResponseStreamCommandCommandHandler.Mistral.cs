#pragma warning disable OPENAI001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Microsoft.SemanticKernel.Connectors.MistralAI.Client;
using MoAI.AI.Models;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// MistralAI 流式处理支持.
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler
{
    private static async IAsyncEnumerable<AiProcessingChatItem> UseMistralStream(List<OpenAIChatCompletionsUsage> useages, ProcessingAiAssistantChatContext chatContext, Microsoft.SemanticKernel.StreamingChatMessageContent chunk)
    {
        // 将 InnerContent 转换为 Mistral 的流式响应对象
        if (chunk.InnerContent is not MistralChatCompletionChunk streamingChatCompletionUpdate)
        {
            yield break;
        }

        // Mistral API 在流式响应的最后一条消息中返回使用情况
        if (streamingChatCompletionUpdate.Usage is { } usage)
        {
            useages.Add(new OpenAIChatCompletionsUsage
            {
                CompletionTokens = usage.CompletionTokens ?? 0,
                PromptTokens = usage.PromptTokens ?? 0,
                TotalTokens = usage.TotalTokens ?? 0
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

        var choice = streamingChatCompletionUpdate.Choices?.FirstOrDefault();
        if (choice is null)
        {
            yield break;
        }

        // 检查是否有文本内容更新
        if (!string.IsNullOrEmpty(choice.Delta?.Content?.ToString()))
        {
            // 先结束上一个非文本类型的块
            if (lastChoice != null && lastChoice.StreamType != AiProcessingChatStreamType.Text)
            {
                await foreach (var end in EndChoiceAsync(AiProcessingChatStreamType.Text, lastChoice))
                {
                    yield return end;
                }
            }

            // 如果是新的文本块，或者上一个文本块已结束
            if (lastChoice == null || lastChoice.StreamType != AiProcessingChatStreamType.Text || lastChoice.StreamState is AiProcessingChatStreamState.Error or AiProcessingChatStreamState.End)
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
                    Choices = new List<AiProcessingChoice> { lastChoice.ToAiProcessingChoice() }
                };
            }
            else // 继续上一个文本块
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

        // 检查是否有工具调用更新
        if (choice.Delta?.ToolCalls is { Count: > 0 } toolCallUpdates)
        {
            foreach (var toolCall in toolCallUpdates)
            {
                // 先结束上一个非插件类型的块
                if (lastChoice != null && lastChoice.StreamType != AiProcessingChatStreamType.Plugin)
                {
                    await foreach (var end in EndChoiceAsync(AiProcessingChatStreamType.Plugin, lastChoice))
                    {
                        yield return end;
                    }
                }

                // 上一个不是插件，或者上一个插件已经结束
                if (lastChoice == null || lastChoice.StreamType != AiProcessingChatStreamType.Plugin || lastChoice.StreamState is AiProcessingChatStreamState.Error or AiProcessingChatStreamState.End)
                {
                    if (toolCall.Function is null || string.IsNullOrEmpty(toolCall.Function.Name)) continue;

                    var functionName = toolCall.Function.Name;

                    var pluginType = functionName.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)
                        ? PluginType.WikiPlugin
                        : PluginType.NativePlugin;

                    // 去掉结尾的 _invoke 或 -invoke
                    var pluginKey = functionName.EndsWith("_invoke", StringComparison.CurrentCultureIgnoreCase)
                        ? functionName[..^7]
                        : functionName.EndsWith("-invoke", StringComparison.CurrentCultureIgnoreCase)
                            ? functionName[..^7]
                            : functionName;

                    lastChoice = new DefaultAiProcessingChoice
                    {
                        StreamType = AiProcessingChatStreamType.Plugin,
                        StreamState = AiProcessingChatStreamState.Start,
                        PluginCall = new DefaultAiProcessingPluginCall
                        {
                            ToolCallId = toolCall.Id ?? string.Empty,
                            PluginKey = pluginKey,
                            PluginName = chatContext.PluginKeyNames.FirstOrDefault(x => x.Key == pluginKey).Value ?? string.Empty,
                            FunctionName = functionName,
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
                else // 继续现有的插件调用
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
        }

        // 当流结束时（finish_reason 不为 null），确保最后一个 choice 被标记为结束。
        if (choice.FinishReason is not null && lastChoice is not null && lastChoice.StreamState != AiProcessingChatStreamState.End)
        {
            await foreach (var end in EndChoiceAsync(lastChoice.StreamType, lastChoice))
            {
                yield return end;
            }
        }
    }
}