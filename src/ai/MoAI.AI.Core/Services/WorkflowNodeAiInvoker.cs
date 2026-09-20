using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Services;

/// <summary>
/// <inheritdoc cref="IWorkflowNodeAiInvoker"/>
/// 工作流 AI 节点的统一模型接入实现：模型解析（<see cref="IAiModelResolver"/>，公共/团队授权口径）
/// → 协议客户端（<see cref="IChatClientProvider"/>，按渠道协议族构建）→ 消息组装 → 流式补全；
/// 引入技能或开启沙箱时复用 Agent 工具链（<see cref="AppContextProviderFactory"/> + ChatClientAgent，渐进式披露）.
/// </summary>
[InjectOnScoped]
public sealed class WorkflowNodeAiInvoker : IWorkflowNodeAiInvoker
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowNodeAiInvoker"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="modelResolver">团队模型解析器.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="contextProviderFactory">Agent 上下文提供者工厂（技能/沙箱工具装配）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public WorkflowNodeAiInvoker(
        DatabaseContext databaseContext,
        IAiModelResolver modelResolver,
        IChatClientProvider chatClientProvider,
        AppContextProviderFactory contextProviderFactory,
        ILoggerFactory loggerFactory)
    {
        _databaseContext = databaseContext;
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _contextProviderFactory = contextProviderFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public async Task<string> ChatAsync(WorkflowNodeChatRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken = default)
    {
        var resolved = await _modelResolver.ResolveByIdAsync(request.ModelId, request.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException($"模型 {request.ModelId} 不存在或该团队不可用") { StatusCode = 400 };

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);

        // 引入了技能或开启沙箱：走 Agent 工具链（list_tools/call_tool 渐进式披露 + 多轮工具调用）
        if (request.SkillIds.Count > 0 || request.SandboxEnabled)
        {
            return await ChatWithToolsAsync(chatClient, request, onProgress, cancellationToken).ConfigureAwait(false);
        }

        var options = request.Temperature.HasValue ? new ChatOptions { Temperature = request.Temperature.Value } : null;
        var result = new StringBuilder();
        await foreach (var update in chatClient.GetStreamingResponseAsync(BuildMessages(request), options, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta).ConfigureAwait(false);
            }
        }

        return result.ToString();
    }

    /// <inheritdoc/>
    public async Task<string> AgentAppAsync(WorkflowNodeAgentAppRequest request, CancellationToken cancellationToken = default)
    {
        var resolved = await _modelResolver.ResolveByIdAsync(request.Config.ModelId, request.App.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("Agent 应用的对话模型不可用，请重新配置") { StatusCode = 400 };
        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);

        var buildContext = new AppAgentBuildContext
        {
            App = request.App,
            Config = request.Config,
            AppId = request.App.Id,
            TeamId = request.App.TeamId,
            UserId = request.UserId,
            SessionId = Guid.CreateVersion7(),
            WikiIds = ParseWikiIds(request.Config.WikiIds),
            PluginIds = ParseGuidList(request.Config.Plugins),
            WorkflowAppIds = ParseGuidList(request.Config.WorkflowApps),
            SkillIds = ParseGuidList(request.Config.Skills),
            ToolApprovalMode = AppToolApprovalContract.ModeAuto,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = $"workflow-agent-{request.App.Id:N}",
                Name = string.IsNullOrWhiteSpace(request.App.Name) ? "agent" : request.App.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(request.Config.Prompt) ? null : request.Config.Prompt,
                },
                ChatHistoryProvider = new InlineChatHistoryProvider(request.History),
                AIContextProviders = contextProviders,
            },
            _loggerFactory);

        var result = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(request.Prompt, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
        }

        return result.ToString();
    }

    private async Task<string> ChatWithToolsAsync(
        IChatClient chatClient,
        WorkflowNodeChatRequest request,
        Func<string, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("工作流执行上下文缺少应用信息，无法装配工具") { StatusCode = 400 };

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
            TeamId = request.TeamId,
            UserId = request.UserId,
            SessionId = Guid.CreateVersion7(),
            WikiIds = [],
            PluginIds = [],
            WorkflowAppIds = [],
            SkillIds = request.SkillIds,
            ToolApprovalMode = AppToolApprovalContract.ModeAuto,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

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

        var result = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(request.Prompt, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta).ConfigureAwait(false);
            }
        }

        return result.ToString();
    }

    private static List<ChatMessage> BuildMessages(WorkflowNodeChatRequest request)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, request.SystemPrompt));
        }

        if (request.History != null)
        {
            messages.AddRange(request.History);
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Prompt));
        return messages;
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

    /// <summary>解析 JSON Guid 数组（插件/技能/流程工具 id，非法值过滤）.</summary>
    private static IReadOnlyList<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            var result = new List<Guid>();
            foreach (var item in raw)
            {
                if (Guid.TryParse(item, out var id) && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
