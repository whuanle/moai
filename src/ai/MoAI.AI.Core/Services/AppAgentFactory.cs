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
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Services;

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
    private readonly ISkillService _skillService;
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
    /// <param name="skillService">技能领域服务.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public AppAgentFactory(
        DatabaseContext databaseContext,
        IAiModelResolver modelResolver,
        IChatClientProvider chatClientProvider,
        AppChatHotStore hotStore,
        IAiModelUsageCounter usageCounter,
        AppContextProviderFactory contextProviderFactory,
        ISkillService skillService,
        ILoggerFactory loggerFactory)
    {
        _databaseContext = databaseContext;
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _hotStore = hotStore;
        _usageCounter = usageCounter;
        _contextProviderFactory = contextProviderFactory;
        _skillService = skillService;
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
    /// <returns>内层 Agent.</returns>
    public async Task<AIAgent> CreateAsync(Guid appId, int teamId, long userId, Guid sessionId, bool isDebug, int promptId, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == appId, cancellationToken).ConfigureAwait(false);
        if (app == null || app.TeamId != teamId)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var config = await _databaseContext.AppAgentConfigs.FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken).ConfigureAwait(false);
        if (config == null || config.ModelId == Guid.Empty)
        {
            throw new BusinessException("应用尚未配置对话模型，无法对话.") { StatusCode = 400 };
        }

        var pair = await _modelResolver.ResolveByIdAsync(config.ModelId, teamId, cancellationToken).ConfigureAwait(false);
        if (pair == null)
        {
            throw new BusinessException("应用的对话模型不可用，请重新配置.") { StatusCode = 400 };
        }

        var inner = await _chatClientProvider.GetChatClientAsync(pair.Value.Model, pair.Value.Channel, cancellationToken).ConfigureAwait(false);

        // 调试会话不计入用量，避免污染应用的监控统计
        IChatClient chatClient = isDebug
            ? inner
            : new UsageCapturingChatClient(inner, _usageCounter, _hotStore, pair.Value.Model.Id, teamId, userId, appId, sessionId);

        var history = new PostgresChatHistoryProvider(_hotStore, _databaseContext, sessionId);

        // 用户级应用配置：自选技能与应用绑定技能取并集生效；调试会话不查（保持应用默认视角）.
        // 应用绑定技能是应用所有者锁定的，不做用户可见性过滤；用户自选技能过滤后静默剔除失效项.
        var lockedSkillIds = ParsePluginIds(config.Skills);
        var userSkillIds = new List<Guid>();
        if (!isDebug)
        {
            var userConfig = await _databaseContext.AppUserConfigs.AsNoTracking()
                .Where(x => x.AppId == appId && x.UserId == userId)
                .Select(x => new { x.Skills })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (userConfig != null)
            {
                var candidates = ParsePluginIds(userConfig.Skills);
                if (candidates.Count > 0)
                {
                    var visible = await _skillService.FilterVisibleSkillIdsAsync(candidates, userId, teamId, cancellationToken).ConfigureAwait(false);
                    userSkillIds.AddRange(visible);
                }
            }
        }

        var buildContext = new AppAgentBuildContext
        {
            App = app,
            Config = config,
            AppId = appId,
            TeamId = teamId,
            UserId = userId,
            SessionId = sessionId,
            WikiIds = ParseWikiIds(config.WikiIds),
            PluginIds = ParsePluginIds(config.Plugins),
            SkillIds = lockedSkillIds.Concat(userSkillIds).Distinct().ToList(),
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        // 会话绑定的专家提示词追加在应用提示词之后；提示词已被删除时静默降级为仅应用提示词
        var expertPrompt = promptId == 0
            ? null
            : await _databaseContext.Prompts
                .Where(x => x.Id == promptId)
                .Select(x => x.Content)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var instructions = config.Prompt;
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

    private static IReadOnlyList<long> ParseWikiIds(string? json)
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
