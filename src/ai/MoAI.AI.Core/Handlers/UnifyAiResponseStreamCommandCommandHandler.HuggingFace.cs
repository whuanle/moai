#pragma warning disable OPENAI001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Microsoft.SemanticKernel.Connectors.HuggingFace;
using MoAI.AI.HuggingFace;
using MoAI.AI.Models;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// HuggingFace 流式处理支持.
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler
{
    private static async IAsyncEnumerable<AiProcessingChatItem> UseHuggingFaceStream(List<OpenAIChatCompletionsUsage> useages, ProcessingAiAssistantChatContext chatContext, Microsoft.SemanticKernel.StreamingChatMessageContent chunk)
    {
        // 将 InnerContent 转换为 HuggingFace 的流式响应对象
        if (chunk.InnerContent is not ChatCompletionStreamResponse streamingChatCompletionUpdate)
        {
            yield break;
        }

        var lastChoice = chatContext.Choices.LastOrDefault();
        var choice = streamingChatCompletionUpdate.Choices?.FirstOrDefault();

        // 从元数据中提取使用情况信息。HuggingFace 通常在流的最后一条消息中提供此信息。
        if (chunk.Metadata is HuggingFaceChatCompletionMetadata metadata && metadata.UsagePromptTokens.HasValue)
        {
            useages.Add(new OpenAIChatCompletionsUsage
            {
                CompletionTokens = metadata.UsageCompletionTokens ?? 0,
                PromptTokens = metadata.UsagePromptTokens.Value,
                TotalTokens = metadata.UsageTotalTokens ?? 0
            });
        }

        // 检查是否有文本内容更新
        if (!string.IsNullOrEmpty(choice?.Delta?.Content))
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
                            Id = lastChoice.Id,
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

        // 当流结束时（finish_reason 不为 null），确保最后一个 choice 被标记为结束。
        if (choice?.FinishReason is not null && lastChoice is not null && lastChoice.StreamState != AiProcessingChatStreamState.End)
        {
            await foreach (var end in EndChoiceAsync(lastChoice.StreamType, lastChoice))
            {
                yield return end;
            }
        }
    }
}