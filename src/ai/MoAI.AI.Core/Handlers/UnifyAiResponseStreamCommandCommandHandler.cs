using MediatR;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using System.Runtime.CompilerServices;

namespace MoAI.AI.Handlers;

/// <summary>
/// <inheritdoc cref="UnifyAiResponseStreamCommand"/>
/// </summary>
internal partial class UnifyAiResponseStreamCommandCommandHandler : IStreamRequestHandler<UnifyAiResponseStreamCommand, AiProcessingChatItem>
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<AiProcessingChatItem> Handle(UnifyAiResponseStreamCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<OpenAIChatCompletionsUsage> useages = request.ChatContext.Usage;
        var chatContext = request.ChatContext;

        await foreach (var chunk in request.ResponseStream)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (request.ChatContext.AiModel.Provider == AiProvider.Azure || request.ChatContext.AiModel.Provider == AiProvider.Openai)
            {
                await foreach (var item in UseOpenAiStream(useages, chatContext, chunk))
                {
                    yield return item;
                }
            }
            else if (request.ChatContext.AiModel.Provider == AiProvider.Anthropic)
            {
                await foreach (var item in UseAnthropicStream(useages, chatContext, chunk))
                {
                    yield return item;
                }
            }
            else if (request.ChatContext.AiModel.Provider == AiProvider.Google)
            {
                await foreach (var item in UseGoogleStream(useages, chatContext, chunk))
                {
                    yield return item;
                }
            }
            else if (request.ChatContext.AiModel.Provider == AiProvider.Huggingface)
            {
                await foreach (var item in UseHuggingFaceStream(useages, chatContext, chunk))
                {
                    yield return item;
                }
            }
            else if (request.ChatContext.AiModel.Provider == AiProvider.Mistral)
            {
                await foreach (var item in UseMistralStream(useages, chatContext, chunk))
                {
                    yield return item;
                }
            }
            else if (request.ChatContext.AiModel.Provider == AiProvider.Ollama)
            {
                await foreach (var item in UseOllamaStream(useages, chatContext, chunk))
                {
                    yield return item;
                }
            }
            else
            {
                throw new NotSupportedException($"不支持的AI提供商：{request.ChatContext.AiModel.Provider}");
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
            PromptTokens = useages.Sum(x => x.PromptTokens),
            CompletionTokens = useages.Sum(x => x.CompletionTokens),
            TotalTokens = useages.Sum(x => x.TotalTokens)
        };

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
    }

    // 结束上一个块
    private static async IAsyncEnumerable<AiProcessingChatItem> EndChoiceAsync(AiProcessingChatStreamType exclude, DefaultAiProcessingChoice choice)
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
}
