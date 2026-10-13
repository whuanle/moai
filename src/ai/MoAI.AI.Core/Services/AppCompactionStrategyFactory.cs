using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.AIChannel.Services;
using MoAI.Database.Entities;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 应用上下文压缩策略工厂：按 execution_settings 组装「工具结果折叠 → 摘要（可选）→ 滑动窗口 → 紧急截断」管线，
/// 压缩参数按「应用显式配置 &gt; 系统默认（AGENT_COMPACTION_DEFAULTS 设置项）&gt; 内置常量」字段级合并.
/// </summary>
[InjectOnScoped]
public sealed class AppCompactionStrategyFactory
{
    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IAgentSettingsService _agentSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppCompactionStrategyFactory"/> class.
    /// </summary>
    /// <param name="modelResolver">模型解析.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="agentSettingsService">Agent 运行时系统默认参数读取服务.</param>
    public AppCompactionStrategyFactory(IAiModelResolver modelResolver, IChatClientProvider chatClientProvider, IAgentSettingsService agentSettingsService)
    {
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _agentSettingsService = agentSettingsService;
    }

    /// <summary>
    /// 构建压缩管线.
    /// </summary>
    /// <param name="config">应用配置，可为空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>压缩策略.</returns>
    public async Task<CompactionStrategy> BuildAsync(AppAgentConfigEntity? config, CancellationToken cancellationToken = default)
    {
        var defaults = await _agentSettingsService.GetAgentCompactionDefaultsAsync(cancellationToken).ConfigureAwait(false);
        var settings = ResolveSettings(config?.ExecutionSettings, defaults);
        var strategies = new List<CompactionStrategy>
        {
            new ToolResultCompactionStrategy(CompactionTriggers.MessagesExceed(settings.ToolResultTriggerMessages)),
        };

        if (settings.EnableSummarization)
        {
            var summarizer = await TryResolveSummarizerAsync(config, settings, cancellationToken).ConfigureAwait(false);
            if (summarizer != null)
            {
                strategies.Add(new SummarizationCompactionStrategy(summarizer, CompactionTriggers.TokensExceed(settings.SummarizeTriggerTokens)));
            }
        }

        strategies.Add(new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(settings.PreserveTurns)));
        strategies.Add(new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(settings.HardTokenLimit)));

        return new PipelineCompactionStrategy(strategies);
    }

    /// <summary>
    /// 字段级合并压缩参数：应用 execution_settings 显式携带的字段覆盖系统默认，未携带的字段用系统默认.
    /// </summary>
    /// <param name="executionSettingsJson">应用执行参数 JSON.</param>
    /// <param name="defaults">系统默认压缩参数.</param>
    /// <returns>合并后的压缩参数.</returns>
    private static AppAgentExecutionSettings ResolveSettings(string? executionSettingsJson, AgentCompactionDefaultsSettings defaults)
    {
        var settings = new AppAgentExecutionSettings
        {
            ToolResultTriggerMessages = defaults.ToolResultTriggerMessages,
            SummarizeTriggerTokens = defaults.SummarizeTriggerTokens,
            PreserveTurns = defaults.PreserveTurns,
            HardTokenLimit = defaults.HardTokenLimit,
            EnableSummarization = defaults.EnableSummarization,
            SummarizerModelId = defaults.SummarizerModelId,
        };

        if (string.IsNullOrWhiteSpace(executionSettingsJson))
        {
            return settings;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(executionSettingsJson) as JsonObject;
        }
        catch (JsonException)
        {
            return settings;
        }

        if (root == null)
        {
            return settings;
        }

        if (TryGetInt(root, "toolResultTriggerMessages", out var toolResultTrigger))
        {
            settings.ToolResultTriggerMessages = toolResultTrigger!.Value;
        }

        if (TryGetInt(root, "summarizeTriggerTokens", out var summarizeTrigger))
        {
            settings.SummarizeTriggerTokens = summarizeTrigger!.Value;
        }

        if (TryGetInt(root, "preserveTurns", out var preserveTurns))
        {
            settings.PreserveTurns = preserveTurns!.Value;
        }

        if (TryGetInt(root, "hardTokenLimit", out var hardLimit))
        {
            settings.HardTokenLimit = hardLimit!.Value;
        }

        if (root["enableSummarization"] is JsonNode enable && enable.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            settings.EnableSummarization = enable.GetValue<bool>();
        }

        if (root["summarizerModelId"] is JsonNode summarizer && summarizer.GetValueKind() == JsonValueKind.String)
        {
            settings.SummarizerModelId = summarizer.GetValue<string>();
        }

        return settings;
    }

    private static bool TryGetInt(JsonObject root, string name, out int? value)
    {
        value = null;
        if (root[name] is JsonNode node && node.GetValueKind() == JsonValueKind.Number && node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private async Task<IChatClient?> TryResolveSummarizerAsync(AppAgentConfigEntity? config, AppAgentExecutionSettings settings, CancellationToken cancellationToken)
    {
        if (config == null)
        {
            return null;
        }

        var modelId = config.ModelId;
        if (Guid.TryParse(settings.SummarizerModelId, out var id) && id != Guid.Empty)
        {
            modelId = id;
        }

        var pair = await _modelResolver.ResolveByIdAsync(modelId, config.TeamId, cancellationToken).ConfigureAwait(false);
        if (pair == null)
        {
            return null;
        }

        return await _chatClientProvider.GetChatClientAsync(pair.Value.Model, pair.Value.Channel, cancellationToken).ConfigureAwait(false);
    }
}
