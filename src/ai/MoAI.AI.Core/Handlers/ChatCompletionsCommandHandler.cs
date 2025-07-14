// <copyright file="ChatCompletionsCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Abstract;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.Infra.Exceptions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MoAI.AI.Handlers;

/// <summary>
/// 聊天生成.
/// </summary>
public class ChatCompletionsCommandHandler : IStreamRequestHandler<ChatCompletionsCommand, IOpenAIChatCompletionsObject>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCompletionsCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    public ChatCompletionsCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IOpenAIChatCompletionsObject> Handle(ChatCompletionsCommand request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        var chatCompletionConfigurator = _serviceProvider.GetKeyedService<IChatCompletionConfigurator>(request.Endpoint.Provider);
        if (chatCompletionConfigurator == null)
        {
            throw new BusinessException("暂不支持该模型");
        }

        var kernel = chatCompletionConfigurator.Configure(kernelBuilder, request.Endpoint)
            .Build();

        StringBuilder chatMessage = new StringBuilder();

        // TODO: 添加知识库
        // TODO: 添加插件
        // TODO: 影响因素
        // TODO: 保存历史记录
        // todo: 后续加上 try ，断开连接后继续处理后续代码
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new PromptExecutionSettings()
        {
            ModelId = request.Endpoint.Name,
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

        // 流式
        var responseStream = chatCompletionService.GetStreamingChatMessageContentsAsync(
            chatHistory: request.ChatHistory,
            kernel: kernel,
            executionSettings: executionSettings,
            cancellationToken: cancellationToken);

        var responseContent = new System.Text.StringBuilder();
        Stopwatch stopwatch = Stopwatch.StartNew();

        await foreach (var chunk in responseStream)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            // todo: 兼容 openai 的接口，openai、azure、deepseek，其它 google 的接口待测试
            var streamingChatCompletionUpdate = chunk.InnerContent as OpenAI.Chat.StreamingChatCompletionUpdate;
            if (streamingChatCompletionUpdate == null)
            {
                // todo: 异常
                continue;
            }

            var finishReason = streamingChatCompletionUpdate.FinishReason;

            if (streamingChatCompletionUpdate.FinishReason != null && streamingChatCompletionUpdate.FinishReason == OpenAI.Chat.ChatFinishReason.Stop)
            {
                var useage = streamingChatCompletionUpdate.Usage;

                yield return new OpenAIChatCompletionsObject
                {
                    Model = request.Endpoint.Name,
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    SystemFingerprint = streamingChatCompletionUpdate.SystemFingerprint,
                    Id = streamingChatCompletionUpdate.CompletionId,
                    Choices = new List<OpenAIChatCompletionsChoice>
                    {
                        new OpenAIChatCompletionsChoice
                        {
                            Index = chunk.ChoiceIndex,
                            FinishReason = streamingChatCompletionUpdate.FinishReason!.ToString()!.ToLower(),
                            Message = new OpenAIChatCompletionsDelta
                            {
                                Role = (chunk.Role ?? AuthorRole.Assistant).ToString().ToLower(),
                                Content = responseContent.ToString(),
                            }
                        }
                    },
                };
            }

            responseContent.Append(chunk.Content);

            yield return new OpenAIChatCompletionsChunk
            {
                Model = request.Endpoint.Name,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SystemFingerprint = streamingChatCompletionUpdate.SystemFingerprint,
                Id = streamingChatCompletionUpdate.CompletionId,
                Choices = new List<OpenAIChatCompletionsChoice>
                    {
                        new OpenAIChatCompletionsChoice
                        {
                            Index = chunk.ChoiceIndex,
                            FinishReason = null,
                            Delta = new OpenAIChatCompletionsDelta
                            {
                                Content = chunk.Content ?? string.Empty,
                                Role = chunk.Role?.ToString() ?? "assistant"
                            }
                        }
                    },
            };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            yield return new OpenAIChatCompletionsObject
            {
                Id = request.ChatId.ToString(),
            };
        }
    }
}
