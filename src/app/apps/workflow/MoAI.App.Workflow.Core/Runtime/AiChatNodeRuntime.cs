using Maomi;
using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// AiChat 节点运行时实现.
/// AiChat 节点负责调用 AI 模型进行对话，支持配置提示词、对话历史和执行参数.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.AiChat)]
public class AiChatNodeRuntime : INodeRuntime
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiChatNodeRuntime"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者，用于发送命令和查询.</param>
    public AiChatNodeRuntime(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.AiChat;

    /// <summary>
    /// 执行 AiChat 节点逻辑.
    /// 调用 AI 模型服务，使用配置的提示词和参数进行对话.
    /// </summary>
    /// <param name="inputs">节点输入数据，应包含 modelId、prompt 等字段.</param>
    /// <param name="pipeline">节点管道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含 AI 模型响应的执行结果.</returns>
    public async Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("modelId", out var modelIdObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: modelId");
            }

            if (!inputs.TryGetValue("prompt", out var promptObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: prompt");
            }

            // 2. 解析输入参数
            int modelId = Convert.ToInt32(modelIdObj);
            string prompt = promptObj?.ToString() ?? string.Empty;

            // 3. 获取 AI 模型端点信息
            AiEndpoint aiEndpoint;
            try
            {
                aiEndpoint = await _mediator.Send(
                    new QueryPublicAiModelToAiEndpointCommand { AiModelId = modelId },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return NodeExecutionResult.Failure($"获取 AI 模型信息失败: {ex.Message}");
            }

            if (aiEndpoint == null)
            {
                return NodeExecutionResult.Failure($"AI 模型不存在或不可用: modelId={modelId}");
            }

            // 4. 构建对话历史
            var chatHistory = new ChatHistory();

            // 添加系统提示词（如果提供）
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                chatHistory.AddSystemMessage(prompt);
            }

            // 添加历史消息（如果提供）
            if (inputs.TryGetValue("messages", out var messagesObj) && messagesObj != null)
            {
                var messages = ParseMessages(messagesObj);
                foreach (var message in messages)
                {
                    chatHistory.Add(message);
                }
            }

            // 添加用户消息（如果提供）
            if (inputs.TryGetValue("userMessage", out var userMessageObj) && userMessageObj != null)
            {
                string userMessage = userMessageObj.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(userMessage))
                {
                    chatHistory.AddUserMessage(userMessage);
                }
            }

            // 如果没有任何消息，返回错误
            if (chatHistory.Count == 0)
            {
                return NodeExecutionResult.Failure("对话历史为空，请提供 prompt、messages 或 userMessage");
            }

            // 5. 解析执行设置（可选）
            var executionSettings = new List<KeyValueString>();
            if (inputs.TryGetValue("executionSettings", out var settingsObj) && settingsObj != null)
            {
                executionSettings = ParseExecutionSettings(settingsObj);
            }

            // 6. 调用 AI 模型
            var chatCommand = new ChatCompletionsCommand
            {
                ChatId = Guid.NewGuid(),
                Endpoint = aiEndpoint,
                ChatHistory = chatHistory,
                Plugins = Array.Empty<Microsoft.SemanticKernel.KernelPlugin>(),
                ExecutionSetting = executionSettings
            };

            // 收集所有响应块
            var responseContent = new System.Text.StringBuilder();
            OpenAIChatCompletionsUsage? usage = null;

            await foreach (var chunk in _mediator.CreateStream(chatCommand, cancellationToken))
            {
                if (chunk is OpenAIChatCompletionsChunk chunkObj)
                {
                    // 累积响应内容
                    if (chunkObj.Choices?.Count > 0)
                    {
                        var delta = chunkObj.Choices.First().Delta;
                        var content = delta?.Content?.ToString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            responseContent.Append(content);
                        }
                    }
                }
                else if (chunk is OpenAIChatCompletionsObject completionObj)
                {
                    // 最终响应，包含完整内容和使用统计
                    if (completionObj.Choices?.Count > 0)
                    {
                        var message = completionObj.Choices.First().Message;
                        var content = message?.Content?.ToString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            responseContent.Clear();
                            responseContent.Append(content);
                        }
                    }
                    usage = completionObj.Usage;
                }
            }

            // 7. 构建输出
            var output = new Dictionary<string, object>
            {
                ["content"] = responseContent.ToString(),
                ["model"] = aiEndpoint.Name,
                ["modelId"] = modelId
            };

            // 添加使用统计（如果可用）
            if (usage != null)
            {
                output["usage"] = new Dictionary<string, object>
                {
                    ["promptTokens"] = usage.PromptTokens,
                    ["completionTokens"] = usage.CompletionTokens,
                    ["totalTokens"] = usage.TotalTokens
                };
            }

            return NodeExecutionResult.Success(output);
        }
        catch (BusinessException bex)
        {
            return NodeExecutionResult.Failure($"业务异常: {bex.Message}");
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure(ex);
        }
    }

    /// <summary>
    /// 解析消息列表.
    /// </summary>
    private List<ChatMessageContent> ParseMessages(object messagesObj)
    {
        var messages = new List<ChatMessageContent>();

        try
        {
            if (messagesObj is string messagesJson)
            {
                var messageList = messagesJson.JsonToObject<List<Dictionary<string, string>>>();
                if (messageList != null)
                {
                    foreach (var msg in messageList)
                    {
                        if (msg.TryGetValue("role", out var role) && msg.TryGetValue("content", out var content))
                        {
                            var authorRole = ParseRole(role);
                            messages.Add(new ChatMessageContent(authorRole, content));
                        }
                    }
                }
            }
            else if (messagesObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue("role", out var role) && dict.TryGetValue("content", out var content))
                        {
                            var authorRole = ParseRole(role?.ToString() ?? "user");
                            messages.Add(new ChatMessageContent(authorRole, content?.ToString() ?? string.Empty));
                        }
                    }
                }
            }
        }
        catch
        {
            // 解析失败时返回空列表
        }

        return messages;
    }

    /// <summary>
    /// 解析角色字符串为 AuthorRole.
    /// </summary>
    private AuthorRole ParseRole(string roleStr)
    {
        return roleStr?.ToLowerInvariant() switch
        {
            "system" => AuthorRole.System,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            _ => AuthorRole.User
        };
    }

    /// <summary>
    /// 解析执行设置.
    /// </summary>
    private List<KeyValueString> ParseExecutionSettings(object settingsObj)
    {
        var settings = new List<KeyValueString>();

        try
        {
            if (settingsObj is string settingsJson)
            {
                var settingsList = settingsJson.JsonToObject<List<KeyValueString>>();
                if (settingsList != null)
                {
                    settings.AddRange(settingsList);
                }
            }
            else if (settingsObj is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    settings.Add(new KeyValueString
                    {
                        Key = kvp.Key,
                        Value = kvp.Value?.ToString() ?? string.Empty
                    });
                }
            }
            else if (settingsObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is KeyValueString kvs)
                    {
                        settings.Add(kvs);
                    }
                }
            }
        }
        catch
        {
            // 解析失败时返回空列表
        }

        return settings;
    }
}
