using System.Text.Json.Nodes;
using Maomi;
using Microsoft.Extensions.AI;
using MoAI.AI.Services;
using MoAI.App.Workflow.Nodes;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流 AI 对话端口实现：aiChat / 问题分类节点的引擎端口适配器——
/// 只做引擎契约（<see cref="AiChatRequest"/>，历史为 [{role, content}] JSON）到 AI 模块统一入口
/// （<see cref="IWorkflowNodeAiInvoker"/>）的映射；模型解析、协议客户端构建、消息组装、
/// 技能/沙箱工具链装配全部由 AI 模块统一实现，本类不接入模型.
/// </summary>
[InjectOnScoped]
public class WorkflowAiChatClient : IAiChatClient
{
    private readonly IWorkflowNodeAiInvoker _aiInvoker;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAiChatClient"/> class.
    /// </summary>
    /// <param name="aiInvoker">AI 模块统一模型接入入口.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    public WorkflowAiChatClient(IWorkflowNodeAiInvoker aiInvoker, WorkflowExecutionContext executionContext)
    {
        _aiInvoker = aiInvoker;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public Task<string> CompleteAsync(AiChatRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model) || !Guid.TryParse(request.Model, out var modelId))
        {
            throw new WorkflowException("AI 对话节点未配置模型，请在节点配置的「AI 模型」中选择");
        }

        return _aiInvoker.ChatAsync(new WorkflowNodeChatRequest
        {
            ModelId = modelId,
            TeamId = (int)_executionContext.TeamId,
            AppId = _executionContext.AppId,
            UserId = _executionContext.UserId,
            SystemPrompt = request.SystemPrompt,
            Prompt = request.Prompt,
            History = ConvertHistory(request.History),
            Temperature = request.Temperature,
        }, onProgress, cancellationToken);
    }

    /// <summary>引擎历史契约 [{role, content}] 转模型消息（空内容跳过，非 assistant 一律按 user）.</summary>
    private static List<ChatMessage>? ConvertHistory(JsonArray? history)
    {
        if (history == null)
        {
            return null;
        }

        var messages = new List<ChatMessage>();
        foreach (var item in history.OfType<JsonObject>())
        {
            var role = item["role"]?.GetValue<string>();
            var content = item["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            messages.Add(new ChatMessage(
                string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User,
                content));
        }

        return messages;
    }
}
