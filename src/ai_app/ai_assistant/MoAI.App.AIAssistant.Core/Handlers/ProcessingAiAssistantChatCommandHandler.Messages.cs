#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using MoAI.AI.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Plugins;
using MoAI.Storage.Queries;
using MoAI.Wiki.WikiPlugin;
using ModelContextProtocol.Client;
using System.Threading;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 构建插件.
/// </summary>
public partial class ProcessingAiAssistantChatCommandHandler
{
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

    // 还原对话历史记录
    private static ChatHistory RestoreChatHistory(List<AppAssistantChatHistoryEntity> history, string? prompt)
    {
        ChatHistory chatMessages = new();

        // 添加提示词.
        if (!string.IsNullOrEmpty(prompt))
        {
            chatMessages.AddSystemMessage(prompt);
        }

        // todo: 后期优化
        foreach (var item in history)
        {
            var contents = item.Content.JsonToObject<IReadOnlyCollection<DefaultAiProcessingChoice>>();

            if (item.Role == AuthorRole.User.Label)
            {
                foreach (var content in contents)
                {
                    if (content.TextCall == null)
                    {
                        continue;
                    }

                    // 后期考虑图片
                    chatMessages.AddAssistantMessage(content: content.TextCall.Content);
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
                chatMessages.AddSystemMessage(item.Content);
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
