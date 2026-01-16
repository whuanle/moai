#pragma warning disable OPENAI001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Microsoft.SemanticKernel;
using MoAI.AI.Models;
using OllamaSharp.Models.Chat; // 确保添加此 using 语句
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// Ollama 流式处理支持.
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler
{
    private static async IAsyncEnumerable<AiProcessingChatItem> UseOllamaStream(List<OpenAIChatCompletionsUsage> useages, ProcessingAiAssistantChatContext chatContext, StreamingChatMessageContent chunk)
    {
        // 将 object 类型的 chunk 转换为 Ollama 的流式响应对象
        if (chunk.InnerContent is not OllamaSharp.Models.Chat.ChatResponseStream streamingChatUpdate)
        {
            yield break;
        }

        var lastChoice = chatContext.Choices.LastOrDefault();

        // 检查是否有文本内容更新
        if (!string.IsNullOrEmpty(streamingChatUpdate.Message?.Content))
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
                        Content = streamingChatUpdate.Message.Content,
                        ContentBuilder = new StringBuilder()
                    }
                };
                lastChoice.TextCall.ContentBuilder.Append(streamingChatUpdate.Message.Content);
                chatContext.Choices.Add(lastChoice);

                lastChoice.IsPush = true;
                yield return new AiProcessingChatItem
                {
                    Choices = new List<AiProcessingChoice> { lastChoice.ToAiProcessingChoice() }
                };
            }
            else // 继续上一个文本块
            {
                lastChoice.TextCall!.ContentBuilder.Append(streamingChatUpdate.Message.Content);
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
                                Content = streamingChatUpdate.Message.Content
                            }
                        }
                    }
                };
            }
        }

        // 当流结束时 (Done = true)，Ollama 会在最后一个响应中提供 token 使用情况
        if (streamingChatUpdate.Done && streamingChatUpdate is ChatDoneResponseStream doneStream)
        {
            useages.Add(new OpenAIChatCompletionsUsage
            {
                PromptTokens = doneStream.PromptEvalCount,
                CompletionTokens = doneStream.EvalCount,
                TotalTokens = doneStream.PromptEvalCount + doneStream.EvalCount
            });

            // 确保最后一个 choice 被标记为结束
            if (lastChoice is not null && lastChoice.StreamState != AiProcessingChatStreamState.End)
            {
                await foreach (var end in EndChoiceAsync(lastChoice.StreamType, lastChoice))
                {
                    yield return end;
                }
            }
        }
    }
}