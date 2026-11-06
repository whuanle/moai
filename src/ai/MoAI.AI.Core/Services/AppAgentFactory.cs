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
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.AI.Models;
using MoAI.Storage.Services;
using MoAI.TeamAgent.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 按应用配置装配内层 <see cref="ChatClientAgent"/>：模型 + 提示词 + 会话历史 + 上下文/压缩管线.
/// </summary>
[InjectOnScoped]
public sealed class AppAgentFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DatabaseContext _databaseContext;
    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly AppChatHotStore _hotStore;
    private readonly IAiModelUsageCounter _usageCounter;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly IWorkflowAppChatInvoker _workflowChatInvoker;
    private readonly IWorkflowChatEventSource _workflowChatEventSource;
    private readonly IStorageService _storageService;
    private readonly AppSecurityService _appSecurityService;
    private readonly AppSecurityAuditWriter _appSecurityAuditWriter;
    private readonly SystemOptions _systemOptions;
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
    /// <param name="workflowChatEventSource">流程执行事件源（Workflow 应用对话流式过程推送）.</param>
    /// <param name="storageService">存储服务（对话图片附件多模态注入读取字节）.</param>
    /// <param name="appSecurityService">应用内容脱敏策略读取服务.</param>
    /// <param name="appSecurityAuditWriter">应用安全事件写入器.</param>
    /// <param name="systemOptions">系统配置（Debug=true 时挂 OpenTelemetry 调试埋点）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public AppAgentFactory(
        DatabaseContext databaseContext,
        IAiModelResolver modelResolver,
        IChatClientProvider chatClientProvider,
        AppChatHotStore hotStore,
        IAiModelUsageCounter usageCounter,
        AppContextProviderFactory contextProviderFactory,
        IWorkflowAppChatInvoker workflowChatInvoker,
        IWorkflowChatEventSource workflowChatEventSource,
        IStorageService storageService,
        AppSecurityService appSecurityService,
        AppSecurityAuditWriter appSecurityAuditWriter,
        SystemOptions systemOptions,
        ILoggerFactory loggerFactory)
    {
        _databaseContext = databaseContext;
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _hotStore = hotStore;
        _usageCounter = usageCounter;
        _contextProviderFactory = contextProviderFactory;
        _workflowChatInvoker = workflowChatInvoker;
        _workflowChatEventSource = workflowChatEventSource;
        _storageService = storageService;
        _appSecurityService = appSecurityService;
        _appSecurityAuditWriter = appSecurityAuditWriter;
        _systemOptions = systemOptions;
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
    /// <param name="enableUiTools">是否注册前端展示工具（X-Moai-Ui-Tools=1）：ui_ 前缀工具后端桩执行、前端侧边栏渲染.</param>
    /// <param name="requestedModelId">用户在对话端选择的模型 id（X-Moai-Model-Id 请求头），须在应用可选模型列表内且团队可用，否则回退默认模型.</param>
    /// <param name="requestedTeamAgentId">用户在对话端选择的团队智能体 id（X-Moai-Team-Agent-Id 请求头），须在应用绑定列表内，否则回退默认智能体.</param>
    /// <returns>内层 Agent.</returns>
    public async Task<AIAgent> CreateAsync(Guid appId, int teamId, long userId, Guid sessionId, bool isDebug, int promptId, CancellationToken cancellationToken, string? toolApprovalMode = null, bool workflowDraft = false, bool enableUiTools = false, Guid? requestedModelId = null, Guid? requestedTeamAgentId = null)
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
            var workflowClient = new WorkflowAppChatClient(_workflowChatInvoker, request, _workflowChatEventSource);
            // 脱敏策略作用于消息落库（工具结果在源头由流程引擎/中间件脱敏，此处覆盖正文与参数记录）
            var workflowPolicy = await _appSecurityService.GetPolicyAsync(appId, cancellationToken).ConfigureAwait(false);
            var workflowHistory = new PostgresChatHistoryProvider(_hotStore, _databaseContext, sessionId, workflowPolicy);
            var workflowAgent = new ChatClientAgent(
                workflowClient,
                new ChatClientAgentOptions
                {
                    Id = appId.ToString("N"),
                    Name = AppAgentConstants.AgentName,
                    ChatHistoryProvider = workflowHistory,
                },
                _loggerFactory);
            return _systemOptions.Debug ? AiObservabilityMiddleware.WrapAgent(workflowAgent) : workflowAgent;
        }

        var config = await _databaseContext.AppAgentConfigs.FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken).ConfigureAwait(false);
        if (config == null)
        {
            throw new BusinessException("应用尚未配置对话模型，无法对话.") { StatusCode = 400 };
        }

        // 正式会话按发布快照执行，管理员保存的草稿不影响线上；调试会话与未发布应用按实时草稿
        // （存量已发布应用无快照时回退实时配置，重新发布后进入草稿/发布双轨）
        var effectiveConfig = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: !isDebug);

        // 外部应用面向外部用户/匿名开放，不允许沙箱与技能：保存入口已拒绝新配置，
        // 此处对生效配置（含发布快照与存量草稿）兜底强制关闭，克隆脱管行避免污染变更跟踪
        if (app.IsExternal)
        {
            effectiveConfig = CloneWithExternalRestrictions(effectiveConfig);
        }

        if (effectiveConfig.ModelId == Guid.Empty)
        {
            throw new BusinessException("应用尚未配置对话模型，无法对话.") { StatusCode = 400 };
        }

        // 团队智能体（team-agent Phase 2）：解析本轮生效智能体（用户切换 > 配置默认 > 列表第一个），
        // 已发布按快照内嵌定义、调试按实时行；未绑定或定义缺失时为 null（回退通用子代理行为，不炸会话）
        var activeTeamAgent = await ResolveActiveTeamAgentAsync(effectiveConfig, !isDebug, requestedTeamAgentId, cancellationToken).ConfigureAwait(false);

        // 对话端多模型切换：所选模型须在生效配置的可选列表内且该团队可用，否则静默回退默认模型
        // （管理员调整配置后旧会话的旧选择平滑降级，不炸会话）；默认模型也不可用才拒绝
        var selectableModelIds = ParseSelectableModelIds(effectiveConfig);
        var pair = await ResolveChatModelAsync(selectableModelIds, effectiveConfig.ModelId, requestedModelId, teamId, cancellationToken).ConfigureAwait(false);
        if (pair == null)
        {
            throw new BusinessException("应用的对话模型不可用，请重新配置.") { StatusCode = 400 };
        }

        // 记录本轮实际对话模型与生效团队智能体（对话端切换后回显跟随）；调试会话不落库
        if (!isDebug)
        {
            await UpdateSessionLastModelIdAsync(sessionId, pair.Value.Model.Id, cancellationToken).ConfigureAwait(false);
            if (activeTeamAgent != null)
            {
                await UpdateSessionLastTeamAgentIdAsync(sessionId, activeTeamAgent.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        var inner = await _chatClientProvider.GetChatClientAsync(pair.Value.Model, pair.Value.Channel, cancellationToken).ConfigureAwait(false);

        // Debug 埋点挂最内层：每次模型调用产生携带正文的 Span 与 gen_ai 指标
        if (_systemOptions.Debug)
        {
            inner = AiObservabilityMiddleware.WrapChatClient(inner);
        }

        // 图片附件多模态注入：发给模型前把用户消息中的图片标记块转为 ImageContent（内层最贴近 SDK）；
        // 调试会话不计入用量，避免污染应用的监控统计
        var innerWithImages = new ChatAttachmentImageChatClient(
            inner,
            _storageService,
            _loggerFactory.CreateLogger<ChatAttachmentImageChatClient>());
        IChatClient chatClient = isDebug
            ? innerWithImages
            : new UsageCapturingChatClient(innerWithImages, _usageCounter, _hotStore, pair.Value.Model.Id, teamId, userId, appId, sessionId);

        // 内容脱敏：加载应用安全策略，作用于会话历史落库与 Agent 中间件（工具结果/参数/模型正文）
        var policy = await _appSecurityService.GetPolicyAsync(appId, cancellationToken).ConfigureAwait(false);
        var history = new PostgresChatHistoryProvider(_hotStore, _databaseContext, sessionId, policy);

        // 可选技能池 = 应用绑定 ∪ 市场公开 ∪ 本人个人（排除禁用）；用户勾选与池交集生效，绑定在前（保持配置顺序）；
        // 未保存过用户配置时默认全部启用（绑定集）；调试会话不查用户配置（保持应用默认视角）.
        var boundSkillIds = ParsePluginIds(effectiveConfig.Skills);
        var effectiveSkillIds = boundSkillIds;
        if (!isDebug)
        {
            var userConfig = await _databaseContext.AppUserConfigs.AsNoTracking()
                .Where(x => x.AppId == appId && x.UserId == userId)
                .Select(x => new { x.Skills })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (userConfig != null)
            {
                var selectedOrdered = new List<Guid>();
                var selectedSet = new HashSet<Guid>();
                foreach (var id in ParsePluginIds(userConfig.Skills))
                {
                    if (selectedSet.Add(id))
                    {
                        selectedOrdered.Add(id);
                    }
                }

                var poolIds = (await _databaseContext.Skills.AsNoTracking()
                    .Where(x => selectedSet.Contains(x.Id) && !x.IsDisable
                        && (boundSkillIds.Contains(x.Id) || x.IsPublic
                            || (x.TeamId == 0 && !x.IsSystem && x.CreateUserId == userId)))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                    .ToHashSet();
                var effective = new List<Guid>();
                foreach (var id in boundSkillIds)
                {
                    if (poolIds.Contains(id))
                    {
                        effective.Add(id);
                    }
                }

                foreach (var id in selectedOrdered)
                {
                    if (!boundSkillIds.Contains(id) && poolIds.Contains(id))
                    {
                        effective.Add(id);
                    }
                }

                effectiveSkillIds = effective;
            }
        }

        var buildContext = new AppAgentBuildContext
        {
            App = app,
            Config = effectiveConfig,
            ChatModelId = pair.Value.Model.Id,
            AppId = appId,
            TeamId = teamId,
            UserId = userId,
            SessionId = sessionId,
            WikiIds = ParseWikiIds(effectiveConfig.WikiIds),
            GraphIds = ParseGraphIds(effectiveConfig.GraphIds),
            PluginIds = ParsePluginIds(effectiveConfig.Plugins),
            WorkflowAppIds = ParsePluginIds(effectiveConfig.WorkflowApps),
            AgentAppIds = ParsePluginIds(effectiveConfig.AgentApps),
            SkillIds = effectiveSkillIds,
            ToolApprovalMode = MoAI.AI.AppToolApprovalContract.IsValidMode(toolApprovalMode)
                ? toolApprovalMode!
                : MoAI.AI.AppToolApprovalContract.ModeAuto,
            EnableUiTools = enableUiTools,
            ActiveTeamAgent = activeTeamAgent,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        // 会话绑定的专家提示词追加在应用提示词之后；提示词已被删除时静默降级为仅应用提示词。
        // 团队智能体主提示词按「应用提示词 → 智能体主提示词 → 专家提示词」层级插入：
        // 应用提示词保留应用身份与全局约束，智能体主提示词定义本轮协作角色与工作方式
        var expertPrompt = promptId == 0
            ? null
            : await _databaseContext.Prompts
                .Where(x => x.Id == promptId)
                .Select(x => x.Content)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var instructions = effectiveConfig.Prompt;
        if (activeTeamAgent != null && !string.IsNullOrWhiteSpace(activeTeamAgent.MainPrompt))
        {
            var teamAgentBlock = BuildTeamAgentBlock(activeTeamAgent, effectiveConfig);
            instructions = string.IsNullOrWhiteSpace(instructions)
                ? teamAgentBlock
                : $"{instructions}\n\n{teamAgentBlock}";
        }

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
        // MAF 官方推荐拦截点（AIAgentBuilder.Use）：输入检查点与工具结果在函数调用中间件处理，参数与正文在 Run 级中间件处理
        var securedAgent = AppSecurityAgentMiddleware.Wrap(new ChatClientAgent(chatClient, options, _loggerFactory), policy, _appSecurityAuditWriter);
        // Debug 埋点包在脱敏中间件之外，Span 只记录脱敏后内容
        return _systemOptions.Debug ? AiObservabilityMiddleware.WrapAgent(securedAgent) : securedAgent;
    }

    /// <summary>
    /// 解析生效配置的可选模型列表：行内 model_ids 为空时归一为仅默认模型（存量应用兼容）.
    /// </summary>
    /// <param name="config">生效配置行.</param>
    /// <returns>可选模型 id 列表.</returns>
    private static IReadOnlyList<Guid> ParseSelectableModelIds(AppAgentConfigEntity config)
    {
        var ids = ParsePluginIds(config.ModelIds);
        if (ids.Count == 0)
        {
            ids = new List<Guid> { config.ModelId };
        }

        return ids;
    }

    /// <summary>
    /// 构建团队智能体注入块（绑定与子代理协作解耦，app sdd D53）：绑定即主智能体提示词生效.
    /// 开启子代理协作且有子智能体时附名册（名称/职责 + 派发指引），主代理按描述自主决定派发（opencode 语义）；
    /// 未开启协作或无子智能体时注明独立完成，避免模型虚构派发行为.
    /// </summary>
    /// <param name="teamAgent">本轮生效智能体定义.</param>
    /// <param name="config">生效配置行（读子代理协作开关）.</param>
    /// <returns>注入到系统提示词的文本块.</returns>
    private static string BuildTeamAgentBlock(SnapshotTeamAgent teamAgent, AppAgentConfigEntity config)
    {
        var block = $"[团队智能体：{teamAgent.Name}]\n{teamAgent.MainPrompt}";
        var subAgentEnabled = AppAgentExecutionSettings.Parse(config.ExecutionSettings).SubAgent is { Enabled: true };
        var namedSubs = teamAgent.Subs.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToList();
        if (!subAgentEnabled || namedSubs.Count == 0)
        {
            return $"{block}\n\n[派发说明] 当前未开启子代理协作（或该智能体未配置子智能体），没有子智能体可供派发，请独立完成用户任务，不要尝试委派。";
        }

        var roster = string.Join("\n", namedSubs.Select(x =>
        {
            var name = string.IsNullOrWhiteSpace(x.Name) ? x.Key : x.Name;
            var description = string.IsNullOrWhiteSpace(x.Description) ? "（无职责描述）" : x.Description;
            return $"- {name}（工具名 subagent_{x.Key}）：{description}";
        }));
        return $"{block}\n\n[可派发子智能体]\n{roster}\n" +
            "派发说明：按子智能体职责自主判断是否委派（经 subagent_ 前缀派发工具），无需用户明示；" +
            "用一句明确、可独立完成的任务描述委派，复杂任务拆成多次委派；子智能体在独立上下文中执行并仅回传最终结论，由你整合后答复用户。";
    }

    /// <summary>
    /// 解析本轮生效的团队智能体：绑定列表为空返回 null；激活 id = 请求头选择（须在列表内）> 配置默认 > 列表第一个；
    /// 定义来源优先快照内嵌（正式会话），缺失时按实时行解析（调试会话/存量快照）；已删除或已停用按未绑定处理.
    /// </summary>
    /// <param name="effectiveConfig">生效配置行（草稿或快照克隆）.</param>
    /// <param name="preferPublished">是否正式会话（按快照内嵌定义执行）.</param>
    /// <param name="requestedTeamAgentId">请求头选择的智能体 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>生效智能体定义，未绑定时为 null.</returns>
    private async Task<SnapshotTeamAgent?> ResolveActiveTeamAgentAsync(AppAgentConfigEntity effectiveConfig, bool preferPublished, Guid? requestedTeamAgentId, CancellationToken cancellationToken)
    {
        var selectableIds = ParsePluginIds(effectiveConfig.TeamAgentIds);
        if (selectableIds.Count == 0)
        {
            return null;
        }

        var activeId = requestedTeamAgentId is { } requested && requested != Guid.Empty && selectableIds.Contains(requested)
            ? requested
            : effectiveConfig.DefaultTeamAgentId is { } defaultId && selectableIds.Contains(defaultId)
                ? defaultId
                : selectableIds[0];

        // 正式会话：快照内嵌定义（发布时冻结，编辑团队智能体不影响线上）
        var defs = SnapshotTeamAgent.TryParseDefs(effectiveConfig.TeamAgentDefs);
        if (preferPublished && defs.Count > 0)
        {
            return SnapshotTeamAgent.FindById(defs, activeId);
        }

        // 调试会话或存量快照无内嵌定义：按实时行解析（草稿定义跟随团队智能体编辑）
        var row = await _databaseContext.TeamAgents.AsNoTracking()
            .Where(x => x.Id == activeId && x.Enabled && x.TeamId == effectiveConfig.TeamId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row == null)
        {
            return null;
        }

        return new SnapshotTeamAgent
        {
            Id = row.Id,
            Key = row.Key ?? string.Empty,
            Name = row.Name,
            Description = row.Description ?? string.Empty,
            AvatarObjectKey = row.AvatarObjectKey ?? string.Empty,
            MainPrompt = row.MainPrompt ?? string.Empty,
            MainPluginIds = ParsePluginIds(row.MainPluginIds).ToList(),
            MainWikiIds = ParseWikiIds(row.MainWikiIds).ToList(),
            MainGraphIds = ParseGraphIds(row.MainGraphIds).ToList(),
            Subs = ParseTeamAgentSubs(row.Subs),
        };
    }

    /// <summary>解析 team_agent.subs JSON 为快照子智能体定义（camelCase 写入，Web 口径反序列化）；非法内容按空列表兜底.</summary>
    private static List<SnapshotTeamAgentSub> ParseTeamAgentSubs(string? subsJson)
    {
        if (string.IsNullOrWhiteSpace(subsJson))
        {
            return [];
        }

        try
        {
            var subs = JsonSerializer.Deserialize<List<TeamAgentSubItem>>(subsJson, JsonOptions) ?? [];
            return subs.Select(x => new SnapshotTeamAgentSub
            {
                Key = x.Key ?? string.Empty,
                Name = x.Name ?? string.Empty,
                Description = x.Description ?? string.Empty,
                Prompt = x.Prompt ?? string.Empty,
                PluginIds = x.PluginIds?.ToList() ?? [],
                WikiIds = x.WikiIds?.ToList() ?? [],
                GraphIds = x.GraphIds?.ToList() ?? [],
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 解析本轮对话模型：用户所选模型在可选列表内且团队可用时用之，否则回退默认模型；默认也不可用返回 null.
    /// </summary>
    /// <param name="selectableModelIds">生效配置的可选模型列表.</param>
    /// <param name="defaultModelId">默认模型 id.</param>
    /// <param name="requestedModelId">用户所选模型 id，null 表示未携带（用默认模型）.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模型与渠道.</returns>
    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveChatModelAsync(IReadOnlyList<Guid> selectableModelIds, Guid defaultModelId, Guid? requestedModelId, int teamId, CancellationToken cancellationToken)
    {
        var candidates = new List<Guid>();
        if (requestedModelId is { } requested && requested != Guid.Empty && selectableModelIds.Contains(requested))
        {
            candidates.Add(requested);
        }

        if (defaultModelId != Guid.Empty)
        {
            candidates.Add(defaultModelId);
        }

        foreach (var modelId in candidates)
        {
            var pair = await _modelResolver.ResolveByIdAsync(modelId, teamId, cancellationToken).ConfigureAwait(false);
            if (pair != null)
            {
                return pair;
            }
        }

        return null;
    }

    /// <summary>
    /// 记录本轮实际对话模型到会话行（用量回显跟随对话端切换）；仅在变化时写入，正式会话行不存在时忽略.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="modelId">本轮实际模型 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    private async Task UpdateSessionLastModelIdAsync(Guid sessionId, Guid modelId, CancellationToken cancellationToken)
    {
        var sessionRow = await _databaseContext.AppAgentSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken).ConfigureAwait(false);
        if (sessionRow == null || sessionRow.LastModelId == modelId)
        {
            return;
        }

        sessionRow.LastModelId = modelId;
        await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 记录本轮生效团队智能体到会话行（对话端切换后回显跟随）；仅在变化时写入，正式会话行不存在时忽略.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="teamAgentId">本轮生效团队智能体 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    private async Task UpdateSessionLastTeamAgentIdAsync(Guid sessionId, Guid teamAgentId, CancellationToken cancellationToken)
    {
        var sessionRow = await _databaseContext.AppAgentSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken).ConfigureAwait(false);
        if (sessionRow == null || sessionRow.LastTeamAgentId == teamAgentId)
        {
            return;
        }

        sessionRow.LastTeamAgentId = teamAgentId;
        await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private static IReadOnlyList<long> ParseGraphIds(string? json)
    {
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

    /// <summary>
    /// 外部应用限制克隆：技能清空、沙箱/子代理/记忆关闭、团队智能体解绑，其余字段原样拷贝；返回脱管行，仅用于读取.
    /// </summary>
    /// <param name="config">生效配置行（可能是变更跟踪中的草稿行，不可就地修改）.</param>
    /// <returns>应用了外部应用限制的新配置行.</returns>
    private static AppAgentConfigEntity CloneWithExternalRestrictions(AppAgentConfigEntity config)
    {
        var settings = AppAgentExecutionSettings.Parse(config.ExecutionSettings);
        settings.Sandbox = null;
        settings.SubAgent = null;
        settings.Memory = null;
        return new AppAgentConfigEntity
        {
            Id = config.Id,
            TeamId = config.TeamId,
            AppId = config.AppId,
            Prompt = config.Prompt ?? string.Empty,
            ModelId = config.ModelId,
            ModelIds = config.ModelIds,
            WikiIds = config.WikiIds,
            GraphIds = config.GraphIds,
            Plugins = config.Plugins,
            WorkflowApps = config.WorkflowApps,
            AgentApps = config.AgentApps,
            Skills = "[]",
            ExecutionSettings = JsonSerializer.Serialize(settings, JsonOptions),
            OpeningStatement = config.OpeningStatement ?? string.Empty,
            OpeningStatementEnabled = config.OpeningStatementEnabled,
            QuickInputs = config.QuickInputs,
            TeamAgentIds = "[]",
            DefaultTeamAgentId = null,
            TeamAgentDefs = string.Empty,
            PublishedConfig = config.PublishedConfig,
            Status = config.Status,
        };
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
