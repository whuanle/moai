#pragma warning disable SKEXP0070 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Microsoft.SemanticKernel.Connectors.Google;
using MoAI.AI.Models;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// Google 流式处理支持.
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler
{
    private static async IAsyncEnumerable<AiProcessingChatItem> UseGoogleStream(List<OpenAIChatCompletionsUsage> useages, ProcessingAiAssistantChatContext chatContext, Microsoft.SemanticKernel.StreamingChatMessageContent chunk)
    {
        if (chunk is not GeminiStreamingChatMessageContent geminiChunk)
        {
            yield break;
        }

        if (geminiChunk.Metadata is GeminiMetadata metadata && metadata.TotalTokenCount > 0)
        {
            useages.Add(new OpenAIChatCompletionsUsage
            {
                CompletionTokens = metadata.CandidatesTokenCount,
                PromptTokens = metadata.PromptTokenCount,
                TotalTokens = metadata.TotalTokenCount
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

        // 文本内容处理
        if (!string.IsNullOrEmpty(geminiChunk.Content))
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
                        Content = geminiChunk.Content,
                        ContentBuilder = new StringBuilder(geminiChunk.Content)
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
                lastChoice.TextCall!.ContentBuilder.Append(geminiChunk.Content);
                lastChoice.StreamState = AiProcessingChatStreamState.Processing;
                lastChoice.IsPush = true;

                yield return new AiProcessingChatItem
                {
                    Choices = new List<AiProcessingChoice>
                    {
                        new AiProcessingChoice
                        {
                            Id = lastChoice.Id,
                            StreamType = AiProcessingChatStreamType.Text,
                            StreamState = AiProcessingChatStreamState.Processing,
                            TextCall = new DefaultAiProcessingTextCall
                            {
                                Content = geminiChunk.Content
                            }
                        }
                    }
                };
            }
        }

        // 工具（函数）调用处理
        if (geminiChunk.ToolCalls is { Count: > 0 } toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
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
                    var pluginType = toolCall.FullyQualifiedName.StartsWith("wiki_", StringComparison.OrdinalIgnoreCase)
                        ? PluginType.WikiPlugin
                        : PluginType.NativePlugin;

                    var pluginKey = toolCall.FullyQualifiedName.EndsWith("_invoke", StringComparison.OrdinalIgnoreCase)
                        ? toolCall.FullyQualifiedName[..^7]
                        : toolCall.FullyQualifiedName.EndsWith("-invoke", StringComparison.OrdinalIgnoreCase)
                            ? toolCall.FullyQualifiedName[..^7]
                            : toolCall.FullyQualifiedName;

                    lastChoice = new DefaultAiProcessingChoice
                    {
                        StreamType = AiProcessingChatStreamType.Plugin,
                        StreamState = AiProcessingChatStreamState.Start,
                        PluginCall = new DefaultAiProcessingPluginCall
                        {
                            ToolCallId = string.Empty, // Gemini API 在流式响应中不提供 ToolCallId
                            PluginKey = pluginKey,
                            PluginName = chatContext.PluginKeyNames.FirstOrDefault(x => x.Key == pluginKey).Value ?? string.Empty,
                            FunctionName = toolCall.FullyQualifiedName,
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
        }

        // 检查完成原因
        if (geminiChunk.Metadata is GeminiMetadata finishMetadata && finishMetadata.FinishReason is not null && lastChoice is not null)
        {
            await foreach (var end in EndChoiceAsync(lastChoice.StreamType, lastChoice))
            {
                yield return end;
            }
        }
    }
}