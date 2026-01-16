using Anthropic.Models.Messages;
using Microsoft.SemanticKernel;
using MoAI.AI.Models;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// Anthropic 流式处理支持.
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler
{
    private static async IAsyncEnumerable<AiProcessingChatItem> UseAnthropicStream(List<OpenAIChatCompletionsUsage> usages, ProcessingAiAssistantChatContext chatContext, StreamingChatMessageContent chunk)
    {
        var rawChunk = chunk.InnerContent as RawMessageStreamEvent;
        if (rawChunk == null)
        {
            yield break;
        }

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

        // 消息增量事件，包含使用情况
        if (rawChunk.TryPickDelta(out var messageDelta) && messageDelta.Usage != null)
        {
            usages.Add(new OpenAIChatCompletionsUsage
            {
                CompletionTokens = (int)messageDelta.Usage.OutputTokens,
                PromptTokens = (int)(messageDelta.Usage.InputTokens ?? 0),
                TotalTokens = (int)((messageDelta.Usage.InputTokens ?? 0) + messageDelta.Usage.OutputTokens)
            });
        }

        // 内容块开始事件，处理工具调用
        if (rawChunk.TryPickContentBlockStart(out var contentBlockStart))
        {
            if (contentBlockStart.ContentBlock.TryPickToolUse(out var toolUse))
            {
                // 结束上一个非插件类型的块
                if (lastChoice != null && lastChoice.StreamType != AiProcessingChatStreamType.Plugin)
                {
                    await foreach (var end in EndChoiceAsync(AiProcessingChatStreamType.Plugin, lastChoice))
                    {
                        yield return end;
                    }
                }

                var pluginType = toolUse.Name.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)
                    ? PluginType.WikiPlugin
                    : PluginType.NativePlugin;

                // 去掉结尾的 _invoke 或 -invoke
                var pluginKey = toolUse.Name.EndsWith("_invoke", StringComparison.CurrentCultureIgnoreCase)
                    ? toolUse.Name[..^7]
                    : toolUse.Name.EndsWith("-invoke", StringComparison.CurrentCultureIgnoreCase)
                        ? toolUse.Name[..^7]
                        : toolUse.Name;

                lastChoice = new DefaultAiProcessingChoice
                {
                    StreamType = AiProcessingChatStreamType.Plugin,
                    StreamState = AiProcessingChatStreamState.Start,
                    PluginCall = new DefaultAiProcessingPluginCall
                    {
                        ToolCallId = toolUse.ID,
                        PluginKey = pluginKey,
                        PluginName = chatContext.PluginKeyNames.FirstOrDefault(x => x.Key == pluginKey).Value ?? string.Empty,
                        FunctionName = toolUse.Name,
                        PluginType = pluginType
                    }
                };

                chatContext.Choices.Add(lastChoice);
                lastChoice.IsPush = true;
                yield return new AiProcessingChatItem
                {
                    Choices = new List<AiProcessingChoice> { lastChoice.ToAiProcessingChoice() }
                };
            }
        }

        // 内容块增量事件，处理文本和工具调用参数
        else if (rawChunk.TryPickContentBlockDelta(out var contentBlockDelta))
        {
            // 文本增量
            if (contentBlockDelta.Delta.TryPickText(out var textDelta))
            {
                // 结束上一个非文本类型的块
                if (lastChoice != null && lastChoice.StreamType != AiProcessingChatStreamType.Text)
                {
                    await foreach (var end in EndChoiceAsync(AiProcessingChatStreamType.Text, lastChoice))
                    {
                        yield return end;
                    }
                }

                if (lastChoice == null || lastChoice.StreamType != AiProcessingChatStreamType.Text || lastChoice.StreamState is AiProcessingChatStreamState.Error or AiProcessingChatStreamState.End)
                {
                    lastChoice = new DefaultAiProcessingChoice
                    {
                        StreamType = AiProcessingChatStreamType.Text,
                        StreamState = AiProcessingChatStreamState.Start,
                        TextCall = new DefaultAiProcessingTextCall
                        {
                            Content = textDelta.Text,
                            ContentBuilder = new StringBuilder(textDelta.Text)
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
                    lastChoice.TextCall!.ContentBuilder.Append(textDelta.Text);
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
                                StreamState = lastChoice.StreamState,
                                TextCall = new DefaultAiProcessingTextCall { Content = textDelta.Text }
                            }
                        }
                    };
                }
            }

            // 工具调用输入JSON增量
            else if (contentBlockDelta.Delta.TryPickInputJSON(out var inputJsonDelta))
            {
                if (lastChoice?.StreamType == AiProcessingChatStreamType.Plugin && lastChoice.PluginCall != null)
                {
                    if (lastChoice.StreamState != AiProcessingChatStreamState.End && lastChoice.StreamState != AiProcessingChatStreamState.Error)
                    {
                        lastChoice.StreamState = AiProcessingChatStreamState.Processing;
                        lastChoice.IsPush = true;

                        yield return new AiProcessingChatItem
                        {
                            Choices = new List<AiProcessingChoice> { lastChoice.ToAiProcessingChoice() }
                        };
                    }
                }
            }
        }

        // 消息结束事件
        else if (rawChunk.TryPickStop(out _))
        {
            if (lastChoice != null)
            {
                await foreach (var end in EndChoiceAsync(lastChoice.StreamType, lastChoice))
                {
                    yield return end;
                }
            }
        }
    }
}