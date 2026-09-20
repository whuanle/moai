using System;
using System.Collections.Generic;
using System.Linq;
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
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 按应用配置装配内层 <see cref="ChatClientAgent"/>：模型 + 提示词 + 会话历史 + 上下文/压缩管线.
/// </summary>
[InjectOnScoped]
public sealed class AppAgentFactory
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly AppChatHotStore _hotStore;
    private readonly IAiModelUsageCounter _usageCounter;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly IWorkflowAppChatInvoker _workflowChatInvoker;
    private readonly IStorageService _storageService;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppAgentFactory"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="modelResolver">模型解析.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="usageCounter">模型使用计数器.</param>
    /// <param name="contextProviderFactory">上下文提供者工厂.</param>
    /// <param name="workflowChatInvoker">流程应用对话执行端口（Workflow 应用对话时使用）.</param>
    /// <param name="storageService">存储服务（对话图片附件多模态注入读取字节）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public AppAgentFactory(
        DatabaseContext databaseContext,
        IAiModelResolver modelResolver,
        IChatClientProvider chatClientProvider,
        AppChatHotStore hotStore,
        IAiModelUsageCounter usageCounter,
        AppContextProviderFactory contextProviderFactory,
        IWorkflowAppChatInvoker workflowChatInvoker,
        IStorageService storageService,
        ILoggerFactory loggerFactory)
    {
        _databaseContext = databaseContext;
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _hotStore = hotStore;
        _usageCounter = usageCounter;
        _contextProviderFactory = contextProviderFactory;
        _workflowChatInvoker = workflowChatInvoker;
        _storageService = storageService;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 构建应用 Agent.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="isDebug">是否调试会话：true 时不包裹用量计数器（不计数）.</param>
    /// <param name="promptId">会话绑定的专家提示词 id，0 表示未绑定；内容追加在应用提示词之后.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <param name="toolApprovalMode">工具审批模式（auto/approval，来自对话 SSE 请求头），approval 时重要工具执行前需人工批准.</param>
    /// <param name="workflowDraft">流程应用是否按最新草稿执行（工作台「调试」Tab 请求头 X-Moai-Workflow-Draft）.</param>
    /// <returns>内层 Agent.</returns>
    public async Task<AIAgent> CreateAsync(Guid appId, int teamId, long userId, Guid sessionId, bool isDebug, int promptId, CancellationToken cancellationToken, string? toolApprovalMode = null, bool workflowDraft = false)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == appId, cancellationToken).ConfigureAwait(false);
        if (app == null || app.TeamId != teamId)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        // 流程应用对话：一轮消息 = 一次流程执行（「调试」Tab 按最新草稿免发布，正式对话按已发布快照；见执行端口）
        if (app.AppType == (int)Database.Enums.AppType.Workflow)
        {
            var request = new WorkflowAppChatRequest
            {
                AppId = appId,
                TeamId = teamId,
                UserId = userId,
                SessionId = sessionId,
                UseDraft = workflowDraft,
            };
            var workflowClient = new WorkflowAppChatClient(_workflowChatInvoker, request);
            var workflowHistory = new PostgresChatHistoryProvider(_hotStore, _databaseContext, sessionId);
            return new ChatClientAgent(
                workflowClient,
                new ChatClientAgentOptions
                {
                    Id = appId.ToString("N"),
                    Name = AppAgentConstants.AgentName,
                    ChatHistoryProvider = workflowHistory,
                },
                _loggerFactory);
        }

        var config = await _databaseContext.AppAgentConfigs.FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken).ConfigureAwait(false);
        if (config == null)
        {
            throw new BusinessException("应用尚未配置对话模型，无法对话.") { StatusCode = 400 };
        }

        // 正式会话按发布快照执行，管理员保存的草稿不影响线上；调试会话与未发布应用按实时草稿
        // （存量已发布应用无快照时回退实时配置，重新发布后进入草稿/发布双轨）
        var effectiveConfig = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: !isDebug);

        if (effectiveConfig.ModelId == Guid.Empty)
        {
            throw new BusinessException("应用尚未配置对话模型，无法对话.") { StatusCode = 400 };
        }

        var pair = await _modelResolver.ResolveByIdAsync(effectiveConfig.ModelId, teamId, cancellationToken).ConfigureAwait(false);
        if (pair == null)
        {
            throw new BusinessException("应用的对话模型不可用，请重新配置.") { StatusCode = 400 };
        }

        var inner = await _chatClientProvider.GetChatClientAsync(pair.Value.Model, pair.Value.Channel, cancellationToken).ConfigureAwait(false);

        // 图片附件多模态注入：发给模型前把用户消息中的图片标记块转为 ImageContent（内层最贴近 SDK）；
        // 调试会话不计入用量，避免污染应用的监控统计
        var innerWithImages = new ChatAttachmentImageChatClient(
            inner,
            _storageService,
            _loggerFactory.CreateLogger<ChatAttachmentImageChatClient>());
        IChatClient chatClient = isDebug
            ? innerWithImages
            : new UsageCapturingChatClient(innerWithImages, _usageCounter, _hotStore, pair.Value.Model.Id, teamId, userId, appId, sessionId);

        var history = new PostgresChatHistoryProvider(_hotStore, _databaseContext, sessionId);

        // 应用默认技能由管理员配置，用户可在应用设置中取消勾选（勾选集为默认集的子集）；
        // 未保存过用户配置时默认全部启用；调试会话不查用户配置（保持应用默认视角）.
        var defaultSkillIds = ParsePluginIds(effectiveConfig.Skills);
        var effectiveSkillIds = defaultSkillIds;
        if (!isDebug)
        {
            var userConfig = await _databaseContext.AppUserConfigs.AsNoTracking()
                .Where(x => x.AppId == appId && x.UserId == userId)
                .Select(x => new { x.Skills })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (userConfig != null)
            {
                var selected = ParsePluginIds(userConfig.Skills).ToHashSet();
                effectiveSkillIds = defaultSkillIds.Where(id => selected.Contains(id)).ToList();
            }
        }

        var buildContext = new AppAgentBuildContext
        {
            App = app,
            Config = effectiveConfig,
            AppId = appId,
            TeamId = teamId,
            UserId = userId,
            SessionId = sessionId,
            WikiIds = ParseWikiIds(effectiveConfig.WikiIds),
            PluginIds = ParsePluginIds(effectiveConfig.Plugins),
            WorkflowAppIds = ParsePluginIds(effectiveConfig.WorkflowApps),
            SkillIds = effectiveSkillIds,
            ToolApprovalMode = MoAI.AI.AppToolApprovalContract.IsValidMode(toolApprovalMode)
                ? toolApprovalMode!
                : MoAI.AI.AppToolApprovalContract.ModeAuto,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        // 会话绑定的专家提示词追加在应用提示词之后；提示词已被删除时静默降级为仅应用提示词
        var expertPrompt = promptId == 0
            ? null
            : await _databaseContext.Prompts
                .Where(x => x.Id == promptId)
                .Select(x => x.Content)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var instructions = effectiveConfig.Prompt;
        if (!string.IsNullOrWhiteSpace(expertPrompt))
        {
            instructions = string.IsNullOrWhiteSpace(instructions)
                ? expertPrompt
                : $"{instructions}\n\n{expertPrompt}";
        }

        var options = new ChatClientAgentOptions
        {
            Id = appId.ToString("N"),
            Name = AppAgentConstants.AgentName,
            ChatOptions = new ChatOptions
            {
                Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions,
            },
            ChatHistoryProvider = history,
            AIContextProviders = contextProviders,
        };
        return new ChatClientAgent(chatClient, options, _loggerFactory);
    }

    private static IReadOnlyList<long> ParseWikiIds(string? json)    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<long>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<Guid> ParsePluginIds(string? json)
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
