using System.Text;
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
using MoAI.Database.Enums;
using MoAI.Database.Aggregates;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// Agent 应用调用端口实现：agentApp 节点按发布快照驱动一次 Agent 应用对话（一轮）——
/// 解析生效配置（系统提示词/模型/插件/技能/知识库/流程工具），复用 Agent 工具链装配 ChatClientAgent 执行，
/// 模型按需调用流程工具（WorkflowAppToolProvider 按发布快照驱动子流程），环检测以根流程为基准拦截循环嵌套.
/// </summary>
[InjectOnScoped]
public class WorkflowAgentAppClient : IWorkflowAgentAppClient
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAiModelResolver _aiModelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly WorkflowExecutionContext _executionContext;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAgentAppClient"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="aiModelResolver">团队模型解析器.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="contextProviderFactory">Agent 上下文提供者工厂.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public WorkflowAgentAppClient(
        DatabaseContext databaseContext,
        IAiModelResolver aiModelResolver,
        IChatClientProvider chatClientProvider,
        AppContextProviderFactory contextProviderFactory,
        WorkflowExecutionContext executionContext,
        ILoggerFactory loggerFactory)
    {
        _databaseContext = databaseContext;
        _aiModelResolver = aiModelResolver;
        _chatClientProvider = chatClientProvider;
        _contextProviderFactory = contextProviderFactory;
        _executionContext = executionContext;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public async Task<string> InvokeAsync(Guid agentAppId, string prompt, JsonArray? history, CancellationToken cancellationToken)
    {
        // 运行期环检测防线：以根流程为基准（嵌套执行时 Scoped 上下文的 AppId 已被内层覆盖）
        var rootAppId = WorkflowRootContext.RootAppId ?? _executionContext.AppId;
        await AgentWorkflowCycleGuard.EnsureNoCycleAsync(_databaseContext, rootAppId, [agentAppId], cancellationToken);

        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == agentAppId, cancellationToken)
            ?? throw new WorkflowException("Agent 应用不存在");

        if (app.IsDisable)
        {
            throw new WorkflowException("Agent 应用已被禁用");
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new WorkflowException("所选应用不是 Agent 应用");
        }

        if (app.TeamId != (int)_executionContext.TeamId)
        {
            throw new WorkflowException("Agent 应用不属于本团队");
        }

        if (app.PublishStatus != 1)
        {
            throw new WorkflowException("Agent 应用尚未发布，无法在流程中调用");
        }

        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == agentAppId, cancellationToken)
            ?? throw new WorkflowException("Agent 应用未配置对话模型");

        // 正式调用按发布快照执行（与 Agent 对话/流程工具绑定一致）
        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: true);
        if (effective.ModelId == Guid.Empty)
        {
            throw new WorkflowException("Agent 应用未配置对话模型");
        }

        var resolved = await _aiModelResolver.ResolveByIdAsync(effective.ModelId, app.TeamId, cancellationToken)
            ?? throw new WorkflowException("Agent 应用的对话模型不可用，请重新配置");
        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken);

        var buildContext = new AppAgentBuildContext
        {
            App = app,
            Config = effective,
            AppId = app.Id,
            TeamId = app.TeamId,
            UserId = _executionContext.UserId,
            SessionId = Guid.CreateVersion7(),
            WikiIds = ParseWikiIds(effective.WikiIds),
            PluginIds = AgentWorkflowCycleGuard.ParseGuidList(effective.Plugins),
            WorkflowAppIds = AgentWorkflowCycleGuard.ParseGuidList(effective.WorkflowApps),
            SkillIds = AgentWorkflowCycleGuard.ParseGuidList(effective.Skills),
            ToolApprovalMode = MoAI.AI.AppToolApprovalContract.ModeAuto,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken);

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = $"workflow-agent-{app.Id:N}",
                Name = string.IsNullOrWhiteSpace(app.Name) ? "agent" : app.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(effective.Prompt) ? null : effective.Prompt,
                },
                ChatHistoryProvider = new InlineChatHistoryProvider(history),
                AIContextProviders = contextProviders,
            },
            _loggerFactory);

        var result = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(prompt, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                result.Append(update.Text);
            }
        }

        return result.ToString();
    }

    /// <summary>解析 JSON long 数组（知识库 id，非法/非正数过滤）.</summary>
    private static IReadOnlyList<long> ParseWikiIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<long>>(json) ?? [];
            return raw.Where(id => id > 0).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
