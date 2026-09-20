using System.Text.Json;
using System.Text.Json.Nodes;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Services;
using MoAI.AIChannel.Services;
using MoAI.App.Workflow.Nodes;
using MoAI.Database;
using MoAI.Database.Entities;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流 AI 对话端口实现：aiChat 节点通过 <see cref="IAiModelResolver"/> 解析团队可用模型，
/// 再经 <see cref="IChatClientProvider"/> 构建协议客户端完成对话，流式片段回调给引擎推送进度.
/// 节点 config.aiModelId（兼容旧键 config.model）为模型 id（ai_model.id）字符串；
/// config.skillIds / config.sandboxEnabled 非空时复用 Agent 工具链（技能/沙箱工具，渐进式披露）执行.
/// </summary>
[InjectOnScoped]
public class WorkflowAiChatClient : IAiChatClient
{
    private readonly IAiModelResolver _aiModelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly WorkflowExecutionContext _executionContext;
    private readonly DatabaseContext _databaseContext;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAiChatClient"/> class.
    /// </summary>
    /// <param name="aiModelResolver">团队模型解析器.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="contextProviderFactory">Agent 上下文提供者工厂（技能/沙箱工具装配）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public WorkflowAiChatClient(
        IAiModelResolver aiModelResolver,
        IChatClientProvider chatClientProvider,
        WorkflowExecutionContext executionContext,
        DatabaseContext databaseContext,
        AppContextProviderFactory contextProviderFactory,
        ILoggerFactory loggerFactory)
    {
        _aiModelResolver = aiModelResolver;
        _chatClientProvider = chatClientProvider;
        _executionContext = executionContext;
        _databaseContext = databaseContext;
        _contextProviderFactory = contextProviderFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(AiChatRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model) || !Guid.TryParse(request.Model, out var modelId))
        {
            throw new WorkflowException("AI 对话节点未配置模型，请在节点配置的「AI 模型」中选择");
        }

        var resolved = await _aiModelResolver.ResolveByIdAsync(modelId, (int)_executionContext.TeamId, cancellationToken)
            ?? throw new WorkflowException($"模型 {request.Model} 不存在或该团队不可用");

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken);

        // 引入了技能或开启沙箱：走 Agent 工具链（list_tools/call_tool 渐进式披露 + 多轮工具调用）
        if (request.SkillIds.Count > 0 || request.SandboxEnabled)
        {
            return await CompleteWithToolsAsync(chatClient, request, onProgress, cancellationToken);
        }

        var messages = BuildMessages(request);
        var options = request.Temperature.HasValue ? new ChatOptions { Temperature = request.Temperature.Value } : null;
        var result = new System.Text.StringBuilder();
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, cancellationToken: cancellationToken))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta);
            }
        }

        return result.ToString();
    }

    private async Task<string> CompleteWithToolsAsync(
        IChatClient chatClient,
        AiChatRequest request,
        Func<string, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == _executionContext.AppId, cancellationToken)
            ?? throw new WorkflowException("工作流执行上下文缺少应用信息，无法装配工具");

        // 节点级沙箱开关合成 execution_settings.sandbox（其余沙箱参数用系统默认）
        var config = new AppAgentConfigEntity
        {
            AppId = app.Id,
            ExecutionSettings = JsonSerializer.Serialize(new { sandbox = new { enabled = request.SandboxEnabled } }),
        };

        var buildContext = new AppAgentBuildContext
        {
            App = app,
            Config = config,
            AppId = app.Id,
            TeamId = (int)_executionContext.TeamId,
            UserId = _executionContext.UserId,
            SessionId = Guid.CreateVersion7(),
            WikiIds = [],
            PluginIds = [],
            WorkflowAppIds = [],
            SkillIds = request.SkillIds,
            ToolApprovalMode = MoAI.AI.AppToolApprovalContract.ModeAuto,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken);

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = $"workflow-node-{app.Id:N}",
                Name = "workflow-ai-chat",
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt,
                    Temperature = request.Temperature,
                },
                ChatHistoryProvider = new InlineChatHistoryProvider(request.History),
                AIContextProviders = contextProviders,
            },
            _loggerFactory);

        var result = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(request.Prompt, cancellationToken: cancellationToken))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta);
            }
        }

        return result.ToString();
    }

    private static List<ChatMessage> BuildMessages(AiChatRequest request)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, request.SystemPrompt));
        }

        if (request.History != null)
        {
            foreach (var item in request.History.OfType<JsonObject>())
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
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Prompt));
        return messages;
    }
}
