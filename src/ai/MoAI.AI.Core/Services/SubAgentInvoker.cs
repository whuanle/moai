using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.AIChannel.Services;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;

namespace MoAI.AI.Services;

/// <summary>
/// 子代理调用请求：主代理通过 spawn_subagent 工具委派的任务.
/// </summary>
public class SubAgentInvocationRequest
{
    /// <summary>
    /// 目标应用（即主代理自身所在应用）.
    /// </summary>
    public required AppEntity App { get; init; }

    /// <summary>
    /// 生效配置（主代理当前使用的草稿或发布快照行）.
    /// </summary>
    public required AppAgentConfigEntity Config { get; init; }

    /// <summary>
    /// 触发用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 主会话 id（子代理复用同一沙箱会话，文件互通）.
    /// </summary>
    public Guid ParentSessionId { get; init; }

    /// <summary>
    /// 委派任务描述.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// 执行要求或输出格式约定，可为空.
    /// </summary>
    public string? Guidance { get; init; }

    /// <summary>
    /// 单次子任务超时（秒）.
    /// </summary>
    public int TimeoutSeconds { get; init; } = SubAgentSettings.DefaultTimeoutSeconds;

    /// <summary>
    /// 结果回传字符上限，超长截断.
    /// </summary>
    public int ResultMaxChars { get; init; } = SubAgentSettings.DefaultResultMaxChars;

    /// <summary>
    /// 具名子智能体定义（团队智能体派发，subagent_{key} 工具携带）；null 表示通用子代理（同应用同配置）.
    /// </summary>
    public SnapshotTeamAgentSub? NamedSub { get; init; }

    /// <summary>
    /// 主会话本轮实际对话模型 id（具名子智能体默认跟随该模型）.
    /// </summary>
    public Guid ChatModelId { get; init; }
}

/// <summary>
/// 子代理执行端口：以本应用同配置（模型/提示词/工具链/技能）在全新空上下文中驱动一轮任务，仅最终结论回传.
/// </summary>
public interface ISubAgentInvoker
{
    /// <summary>
    /// 执行一次子代理任务.
    /// </summary>
    /// <param name="request">调用请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>子代理最终结论文本.</returns>
    Task<string> InvokeAsync(SubAgentInvocationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="ISubAgentInvoker"/>
/// 子代理与主代理共用一个 DI 作用域：装配时 <see cref="AppAgentBuildContext.IsSubAgent"/> 置 true，
/// spawn 工具与长期记忆贡献者自动退出，天然保证深度为 1、无递归；子代理不落库、不计审批、不计用量.
/// </summary>
[InjectOnScoped]
public sealed class SubAgentInvoker : ISubAgentInvoker
{
    private const string SubAgentInstructionSuffix =
        "\n\n[子代理模式] 你是主代理委派的子代理，请在独立上下文中一次性完成下述任务：" +
        "直接执行并输出结构化结论（结论 / 关键依据或数据 / 风险与未决问题）；不要向用户提问，" +
        "任务无法完成时说明原因与已尝试的路径。你的输出将被主代理直接引用。";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly AppSecurityService _appSecurityService;
    private readonly AppSecurityAuditWriter _appSecurityAuditWriter;
    private readonly SystemOptions _systemOptions;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubAgentInvoker"/> class.
    /// </summary>
    /// <param name="modelResolver">团队模型解析器.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="contextProviderFactory">上下文提供者工厂（子代理的工具链装配）.</param>
    /// <param name="appSecurityService">应用内容脱敏策略读取服务.</param>
    /// <param name="appSecurityAuditWriter">应用安全事件写入器.</param>
    /// <param name="systemOptions">系统配置（Debug=true 时挂 OpenTelemetry 调试埋点）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public SubAgentInvoker(
        IAiModelResolver modelResolver,
        IChatClientProvider chatClientProvider,
        AppContextProviderFactory contextProviderFactory,
        AppSecurityService appSecurityService,
        AppSecurityAuditWriter appSecurityAuditWriter,
        SystemOptions systemOptions,
        ILoggerFactory loggerFactory)
    {
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _contextProviderFactory = contextProviderFactory;
        _appSecurityService = appSecurityService;
        _appSecurityAuditWriter = appSecurityAuditWriter;
        _systemOptions = systemOptions;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public async Task<string> InvokeAsync(SubAgentInvocationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NamedSub != null)
        {
            return await InvokeNamedSubAsync(request, request.NamedSub, cancellationToken).ConfigureAwait(false);
        }

        var resolved = await _modelResolver.ResolveByIdAsync(request.Config.ModelId, request.App.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("子代理的对话模型不可用，请检查应用模型配置.") { StatusCode = 400 };

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);
        if (_systemOptions.Debug)
        {
            chatClient = AiObservabilityMiddleware.WrapChatClient(chatClient);
        }

        // 子代理无人工审批回路：强制 auto 模式，避免审批闸口挂起等待 300 秒
        var buildContext = new AppAgentBuildContext
        {
            App = request.App,
            Config = request.Config,
            AppId = request.App.Id,
            TeamId = request.App.TeamId,
            UserId = request.UserId,
            // 沿用主会话 id：复用同一沙箱（会话沙箱按 sessionId 创建/续期，文件互通）
            SessionId = request.ParentSessionId,
            WikiIds = ParseLongList(request.Config.WikiIds),
            GraphIds = ParseLongList(request.Config.GraphIds),
            PluginIds = ParseGuidList(request.Config.Plugins),
            WorkflowAppIds = ParseGuidList(request.Config.WorkflowApps),
            AgentAppIds = ParseGuidList(request.Config.AgentApps),
            SkillIds = ParseGuidList(request.Config.Skills),
            ToolApprovalMode = AppToolApprovalContract.ModeAuto,
            IsSubAgent = true,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        var instructions = request.Config.Prompt;
        if (!string.IsNullOrWhiteSpace(request.Guidance))
        {
            instructions = string.IsNullOrWhiteSpace(instructions)
                ? $"执行要求：{request.Guidance}"
                : $"{instructions}\n\n执行要求：{request.Guidance}";
        }

        instructions = (instructions ?? string.Empty) + SubAgentInstructionSuffix;

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = $"subagent-{request.App.Id:N}",
                Name = string.IsNullOrWhiteSpace(request.App.Name) ? "subagent" : request.App.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions,
                },
                ChatHistoryProvider = new InlineChatHistoryProvider(null),
                AIContextProviders = contextProviders,
            },
            _loggerFactory);

        var policy = await _appSecurityService.GetPolicyAsync(request.App.Id, cancellationToken).ConfigureAwait(false);
        var securedAgent = AppSecurityAgentMiddleware.Wrap(agent, policy, _appSecurityAuditWriter);
        if (_systemOptions.Debug)
        {
            securedAgent = AiObservabilityMiddleware.WrapAgent(securedAgent);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 30, 1800)));

        var result = new StringBuilder();
        try
        {
            await foreach (var update in securedAgent.RunStreamingAsync(request.Task, cancellationToken: timeoutCts.Token).ConfigureAwait(false))
            {
                var delta = update.Text;
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                result.Append(delta);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 仅子任务超时：带回已产出内容并注明截断原因
            result.Append($"\n\n[子任务超时：{request.TimeoutSeconds} 秒内未完成，以上为部分结果.]");
        }

        var text = result.ToString().Trim();
        if (request.ResultMaxChars > 0 && text.Length > request.ResultMaxChars)
        {
            text = text[..request.ResultMaxChars] + "\n…[结果过长已截断]";
        }

        return text;
    }

    /// <summary>
    /// 具名子智能体派发（团队智能体 Phase 2）：按子智能体定义注入提示词与工具白名单——
    /// 模型跟随主会话本轮实际模型；系统提示词 = 应用提示词 + 子智能体提示词块 + 执行要求 + 子代理模式后缀；
    /// 工具按「声明即边界」收缩：仅注入 声明 ∩ 应用绑定 的插件/知识库/图谱，技能/流程/Agent 应用/沙箱全部退出
    /// （深度恒 1 与无记忆由 IsSubAgent 装配保证）.
    /// </summary>
    /// <param name="request">调用请求.</param>
    /// <param name="named">子智能体定义.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>子智能体最终结论文本.</returns>
    private async Task<string> InvokeNamedSubAsync(SubAgentInvocationRequest request, SnapshotTeamAgentSub named, CancellationToken cancellationToken)
    {
        // 模型：跟随主会话本轮实际模型（子智能体未声明独立模型），解析失败回退应用默认模型
        var modelId = request.ChatModelId != Guid.Empty ? request.ChatModelId : request.Config.ModelId;
        var resolved = await _modelResolver.ResolveByIdAsync(modelId, request.App.TeamId, cancellationToken).ConfigureAwait(false)
            ?? await _modelResolver.ResolveByIdAsync(request.Config.ModelId, request.App.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("子智能体的对话模型不可用，请检查应用模型配置.") { StatusCode = 400 };

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);
        if (_systemOptions.Debug)
        {
            chatClient = AiObservabilityMiddleware.WrapChatClient(chatClient);
        }

        // 工具白名单：声明 ∩ 应用绑定（声明超出应用绑定资源时静默收缩，运行时不能比主代理拿到更多）
        var boundPluginIds = ParseGuidList(request.Config.Plugins);
        var boundWikiIds = ParseLongList(request.Config.WikiIds);
        var boundGraphIds = ParseLongList(request.Config.GraphIds);
        var buildContext = new AppAgentBuildContext
        {
            App = request.App,
            // 沙箱不属子智能体可声明资源：剥离沙箱配置（子会话无沙箱工具）
            Config = CloneWithoutSandbox(request.Config),
            ChatModelId = resolved.Model.Id,
            AppId = request.App.Id,
            TeamId = request.App.TeamId,
            UserId = request.UserId,
            // 沿用主会话 id：复用同一沙箱（会话沙箱按 sessionId 创建/续期，文件互通）
            SessionId = request.ParentSessionId,
            WikiIds = named.WikiIds.Where(boundWikiIds.Contains).ToList(),
            GraphIds = named.GraphIds.Where(boundGraphIds.Contains).ToList(),
            PluginIds = named.PluginIds.Where(boundPluginIds.Contains).ToList(),
            WorkflowAppIds = [],
            AgentAppIds = [],
            SkillIds = [],
            ToolApprovalMode = AppToolApprovalContract.ModeAuto,
            IsSubAgent = true,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        // 提示词：应用提示词（全局约束）→ 子智能体块（角色）→ 执行要求 → 子代理模式后缀
        var displayName = string.IsNullOrWhiteSpace(named.Name) ? named.Key : named.Name;
        var instructions = request.Config.Prompt;
        var subBlock = $"[子智能体：{displayName}] {named.Description}\n{named.Prompt}";
        instructions = string.IsNullOrWhiteSpace(instructions) ? subBlock : $"{instructions}\n\n{subBlock}";
        if (!string.IsNullOrWhiteSpace(request.Guidance))
        {
            instructions = $"{instructions}\n\n执行要求：{request.Guidance}";
        }

        instructions += SubAgentInstructionSuffix;

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = $"subagent-{request.App.Id:N}",
                Name = $"subagent-{named.Key}",
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions,
                },
                ChatHistoryProvider = new InlineChatHistoryProvider(null),
                AIContextProviders = contextProviders,
            },
            _loggerFactory);

        var policy = await _appSecurityService.GetPolicyAsync(request.App.Id, cancellationToken).ConfigureAwait(false);
        var securedAgent = AppSecurityAgentMiddleware.Wrap(agent, policy, _appSecurityAuditWriter);
        if (_systemOptions.Debug)
        {
            securedAgent = AiObservabilityMiddleware.WrapAgent(securedAgent);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 30, 1800)));

        var result = new StringBuilder();
        try
        {
            await foreach (var update in securedAgent.RunStreamingAsync(request.Task, cancellationToken: timeoutCts.Token).ConfigureAwait(false))
            {
                var delta = update.Text;
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                result.Append(delta);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 仅子任务超时：带回已产出内容并注明截断原因
            result.Append($"\n\n[子任务超时：{request.TimeoutSeconds} 秒内未完成，以上为部分结果.]");
        }

        var text = result.ToString().Trim();
        if (request.ResultMaxChars > 0 && text.Length > request.ResultMaxChars)
        {
            text = text[..request.ResultMaxChars] + "\n…[结果过长已截断]";
        }

        return text;
    }

    /// <summary>克隆配置并剥离沙箱配置（具名子智能体无沙箱）；返回脱管行，仅用于读取.</summary>
    private static AppAgentConfigEntity CloneWithoutSandbox(AppAgentConfigEntity config)
    {
        var settings = AppAgentExecutionSettings.Parse(config.ExecutionSettings);
        settings.Sandbox = null;
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
            Skills = config.Skills,
            ExecutionSettings = JsonSerializer.Serialize(settings, JsonOptions),
            OpeningStatement = config.OpeningStatement ?? string.Empty,
            OpeningStatementEnabled = config.OpeningStatementEnabled,
            QuickInputs = config.QuickInputs,
            TeamAgentIds = config.TeamAgentIds,
            DefaultTeamAgentId = config.DefaultTeamAgentId,
            TeamAgentDefs = config.TeamAgentDefs,
            PublishedConfig = config.PublishedConfig,
            Status = config.Status,
        };
    }

    /// <summary>解析 JSON long 数组（知识库/图谱 id，非法值过滤）.</summary>
    private static IReadOnlyList<long> ParseLongList(string? json)
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
