#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Chat.Models;
using MoAI.AI.Models;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;

namespace MoAI.AI.Chat;

/// <summary>
/// 统一流式对话抽象.
/// </summary>
public abstract partial class ProcessingChatStreamBase
{
    /// <summary>
    /// 构建问题
    /// </summary>
    /// <param name="chatId"></param>
    /// <param name="question"></param>
    /// <param name="fileKey"></param>
    /// <returns></returns>
    protected virtual async Task<IReadOnlyCollection<DefaultAiProcessingChoice>> GenerateChatProcessingChoice(Guid chatId, string question, string? fileKey)
    {
        List<DefaultAiProcessingChoice> choices = new();

        if (!string.IsNullOrEmpty(question))
        {
            choices.Add(new DefaultAiProcessingChoice
            {
                StreamType = AiProcessingChatStreamType.Text,
                StreamState = AiProcessingChatStreamState.End,
                TextCall = new DefaultAiProcessingTextCall
                {
                    Content = question
                }
            });
        }

        if (!string.IsNullOrEmpty(fileKey))
        {
            var ossObjectKey = $"chat/{chatId}/{fileKey}";

            choices.Add(new DefaultAiProcessingChoice
            {
                StreamType = AiProcessingChatStreamType.File,
                StreamState = AiProcessingChatStreamState.End,
                FileCall = new DefaultAiProcessingFileCall
                {
                    FileKey = fileKey
                }
            });
        }

        return choices;
    }

    // 构建问题
    private async Task<ChatMessageContentItemCollection> GenerateChatMessageContent(Guid chatId, string question, string? fileKey)
    {
        ChatMessageContentItemCollection contents = new();

        if (!string.IsNullOrEmpty(question))
        {
            contents.Add(new TextContent(question));
        }

        if (!string.IsNullOrEmpty(fileKey))
        {
            var ossObjectKey = $"chat/{chatId}/{fileKey}";
            var mimeType = FileStoreHelper.GetMimeType(fileKey);

            var streamResult = await _mediator.Send(new ReadFileStreamCommand { ObjectKey = ossObjectKey });
            using var stream = streamResult.FileStream;
            var memory = await stream.ToReadOnlyMemoryAsync();

            // 判断是不是图片
            if (FileStoreHelper.ImageExtensions.Any(x => fileKey.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                // 图片
                contents.Add(new ImageContent(memory, mimeType));
            }
            else
            {
                // contents.Add(new ImageContent(memory, mimeType));
            }
        }

        if (contents.Count == 0)
        {
            throw new BusinessException("请输入提问");
        }

        return contents;
    }

    // 还原对话历史记录
    private static ChatHistory RestoreChatHistory(IReadOnlyCollection<RoleProcessingChoice> choices, string? prompt)
    {
        ChatHistory chatMessages = new();

        // 添加提示词.
        if (!string.IsNullOrEmpty(prompt))
        {
            chatMessages.AddSystemMessage(prompt);
        }

        // todo: 后期优化
        foreach (var item in choices)
        {
            var contents = item.Choices;

            if (item.Role == AuthorRole.User.Label)
            {
                foreach (var content in contents)
                {
                    if (content.TextCall == null)
                    {
                        continue;
                    }

                    // 后期考虑图片
                    chatMessages.AddUserMessage(content: content.TextCall.Content);
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
                        var arguments = new KernelArguments();

                        foreach (var argument in content.PluginCall.Params)
                        {
                            arguments.Add(argument.Key, argument.Value);
                        }

                        var funcCallContent = new FunctionCallContent(
                            functionName: content.PluginCall.FunctionName,
                            pluginName: content.PluginCall.PluginName,
                            id: content.PluginCall.ToolCallId,
                            arguments: arguments)
                        {
                        };

                        chatMessages.Add(new ChatMessageContent
                        {
                            Role = AuthorRole.Assistant,
                            Items = new ChatMessageContentItemCollection
                            {
                                new FunctionCallContent(
                                    functionName: content.PluginCall.FunctionName,
                                    pluginName: content.PluginCall.PluginName,
                                    id: content.PluginCall.ToolCallId,
                                    arguments: arguments)
                            }
                        });

                        chatMessages.Add(new()
                        {
                            Role = AuthorRole.Tool,
                            Items = new ChatMessageContentItemCollection
                            {
                                new FunctionResultContent(
                                    functionName: content.PluginCall.FunctionName,
                                    pluginName: content.PluginCall.PluginName,
                                    callId: content.PluginCall.ToolCallId,
                                    result: content.PluginCall.Result)
                            }
                        });

                        //chatMessages.Add(new ChatMessageContent
                        //{
                        //    Role = AuthorRole.System,
                        //    AuthorName = AuthorRole.System.Label,
                        //    Content = $"""
                        //    插件名称：{content.PluginCall.PluginName}
                        //    调用函数：{content.PluginCall.FunctionName}
                        //    参数：{string.Join(',', content.PluginCall.Params.Select(x => $"{x.Key}={x.Value}"))}
                        //    结果：{content.PluginCall.Result}
                        //    """
                        //});
                    }
                }
            }
            else if (item.Role == AuthorRole.System.Label)
            {
                //chatMessages.AddSystemMessage(item.Content);
            }
            else if (item.Role == AuthorRole.Tool.Label)
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
                        var arguments = new KernelArguments();

                        foreach (var argument in content.PluginCall.Params)
                        {
                            arguments.Add(argument.Key, argument.Value);
                        }

                        var funcCallContent = new FunctionCallContent(
                            functionName: content.PluginCall.FunctionName,
                            pluginName: content.PluginCall.PluginName,
                            id: content.PluginCall.ToolCallId,
                            arguments: arguments)
                        {
                        };

                        chatMessages.Add(new ChatMessageContent
                        {
                            Role = AuthorRole.Assistant,
                            Items = new ChatMessageContentItemCollection
                            {
                                new FunctionCallContent(
                                    functionName: content.PluginCall.FunctionName,
                                    pluginName: content.PluginCall.PluginName,
                                    id: content.PluginCall.ToolCallId,
                                    arguments: arguments)
                            }
                        });

                        chatMessages.Add(new()
                        {
                            Role = AuthorRole.Tool,
                            Items = new ChatMessageContentItemCollection
                            {
                                new FunctionResultContent(
                                    functionName: content.PluginCall.FunctionName,
                                    pluginName: content.PluginCall.PluginName,
                                    callId: content.PluginCall.ToolCallId,
                                    result: content.PluginCall.Result)
                            }
                        });
                    }
                }
            }
            else
            {
                // 其他角色不处理
                continue;
            }
        }

        return chatMessages;
    }
}
