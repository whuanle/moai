#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Commands;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 构建插件.
/// </summary>
public partial class ProcessingAiAssistantChatCommandHandler
{
    private async Task<IReadOnlyCollection<DefaultAiProcessingChoice>> GenerateQuestion(
        ProcessingAiAssistantChatCommand request, ChatHistory chatMessages)
    {
        ChatMessageContentItemCollection contents = [];
        List<DefaultAiProcessingChoice> choices = [];

        if (!string.IsNullOrEmpty(request.Content))
        {
            contents.Add(new TextContent(request.Content));
            choices.Add(new DefaultAiProcessingChoice
            {
                StreamType = AiProcessingChatStreamType.Text,
                StreamState = AiProcessingChatStreamState.End,
                TextCall = new DefaultAiProcessingTextCall
                {
                    Content = request.Content
                }
            });
        }

        if (!string.IsNullOrEmpty(request.FileKey))
        {
            var ossObjectKey = $"chat/{request.ChatId}/{request.FileKey}";
            var mimeType = FileStoreHelper.GetMimeType(request.FileKey);

            var streamResult = await _mediator.Send(new ReadFileStreamCommand { ObjectKey = ossObjectKey });
            await using var stream = streamResult.FileStream;
            var memory = await stream.ToReadOnlyMemoryAsync();

            // 判断是不是图片
            if (FileStoreHelper.ImageExtensions.Any(
                    x => request.FileKey.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                // 图片
                contents.Add(new ImageContent(memory, mimeType));
            }
            else
            {
                // contents.Add(new ImageContent(memory, mimeType));
            }

            choices.Add(new DefaultAiProcessingChoice
            {
                StreamType = AiProcessingChatStreamType.File,
                StreamState = AiProcessingChatStreamState.End,
                FileCall = new DefaultAiProcessingFileCall
                {
                    FileKey = request.FileKey
                }
            });
        }

        if (contents.Count == 0)
        {
            throw new BusinessException("请输入提问");
        }

        chatMessages.AddUserMessage(contents);

        return choices;
    }

    // 还原对话历史记录
    private static ChatHistory RestoreChatHistory(List<AppAssistantChatHistoryEntity> history, string? prompt)
    {
        ChatHistory chatMessages = [];

        // 添加提示词.
        if (!string.IsNullOrEmpty(prompt))
        {
            chatMessages.AddSystemMessage(prompt);
        }

        // todo: 后期优化
        foreach (var item in history)
        {
            var contents = item.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>();

            if (contents is null)
            {
                continue;
            }

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
                    switch (content.StreamType)
                    {
                        case AiProcessingChatStreamType.Text when content.TextCall != null:
                            chatMessages.AddAssistantMessage(content.TextCall.Content);
                            break;
                        // todo： 要考虑失败的插件调用
                        case AiProcessingChatStreamType.Plugin when content.PluginCall != null:
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
                                Items =
                                [
                                    new FunctionCallContent(
                                        functionName: content.PluginCall.FunctionName,
                                        pluginName: content.PluginCall.PluginName,
                                        id: content.PluginCall.ToolCallId,
                                        arguments: arguments)
                                ]
                            });

                            chatMessages.Add(new ChatMessageContent
                            {
                                Role = AuthorRole.Tool,
                                Items =
                                [
                                    new FunctionResultContent(
                                        functionName: content.PluginCall.FunctionName,
                                        pluginName: content.PluginCall.PluginName,
                                        callId: content.PluginCall.ToolCallId,
                                        result: content.PluginCall.Result)
                                ]
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
                            break;
                        }
                    }
                }
            }
            else if (item.Role == AuthorRole.System.Label)
            {
                chatMessages.AddSystemMessage(item.Content);
            }
            else if (item.Role == AuthorRole.Tool.Label)
            {
                foreach (var content in contents)
                {
                    switch (content.StreamType)
                    {
                        case AiProcessingChatStreamType.Text when content.TextCall != null:
                            chatMessages.AddAssistantMessage(content.TextCall.Content);
                            break;

                        // todo： 要考虑失败的插件调用
                        case AiProcessingChatStreamType.Plugin when content.PluginCall != null:
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
                                arguments: arguments);

                            chatMessages.Add(new ChatMessageContent
                            {
                                Role = AuthorRole.Assistant,
                                Items =
                                [
                                    new FunctionCallContent(
                                        functionName: content.PluginCall.FunctionName,
                                        pluginName: content.PluginCall.PluginName,
                                        id: content.PluginCall.ToolCallId,
                                        arguments: arguments)
                                ]
                            });

                            chatMessages.Add(new ChatMessageContent
                            {
                                Role = AuthorRole.Tool,
                                Items =
                                [
                                    new FunctionResultContent(
                                        functionName: content.PluginCall.FunctionName,
                                        pluginName: content.PluginCall.PluginName,
                                        callId: content.PluginCall.ToolCallId,
                                        result: content.PluginCall.Result)
                                ]
                            });
                            break;
                        }
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