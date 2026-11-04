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

namespace MoAI.AI.Services;

/// <summary>
/// 应用上下文压缩策略工厂：按 execution_settings 的 compactionMode 组装压缩管线.
/// <para>
/// auto（缺省）：阈值按「所选对话模型上下文窗口」动态推导——input budget = ContextWindow − MaxOutput，
/// 工具结果折叠 <see cref="AppCompactionPolicy.ToolEvictionRatio"/> → LLM 摘要 <see cref="AppCompactionPolicy.SummarizeTriggerRatio"/>
/// 触发、压回 <see cref="AppCompactionPolicy.SummarizeTargetRatio"/>（大步长低频压缩，保持 prompt cache 前缀稳定）→
/// 紧急截断 <see cref="AppCompactionPolicy.TruncationRatio"/> 兜底；模型窗口元数据缺失时回落到 settings 绝对值（与历史版本行为一致）。
/// custom：按 execution_settings 显式绝对值运行。
/// 旧版「滑动窗口保留轮数」策略已移除（历史 JSON 中的 preserveTurns 读取容忍、不再使用）。
/// </para>
/// </summary>
[InjectOnScoped]
public sealed class AppCompactionStrategyFactory
{
    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppCompactionStrategyFactory"/> class.
    /// </summary>
    /// <param name="modelResolver">模型解析.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    public AppCompactionStrategyFactory(IAiModelResolver modelResolver, IChatClientProvider chatClientProvider)
    {
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
    }

    /// <summary>
    /// 构建压缩管线.
    /// </summary>
    /// <param name="config">应用配置，可为空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <param name="chatModelId">本轮实际对话模型 id（对话端多模型切换时与所选模型一致）；压缩阈值与摘要模型回退对象按它推导，null 回退配置默认模型.</param>
    /// <returns>压缩策略.</returns>
    public async Task<CompactionStrategy> BuildAsync(AppAgentConfigEntity? config, CancellationToken cancellationToken = default, Guid? chatModelId = null)
    {
        var settings = ResolveSettings(config?.ExecutionSettings);
        var isCustom = string.Equals(settings.CompactionMode, AppCompactionPolicy.ModeCustom, StringComparison.Ordinal);
        var inputBudget = isCustom ? null : await TryResolveInputBudgetAsync(config, cancellationToken, chatModelId).ConfigureAwait(false);

        var strategies = new List<CompactionStrategy>();
        if (inputBudget.HasValue)
        {
            // auto：按所选模型 input budget 的比例推导阈值
            strategies.Add(new ToolResultCompactionStrategy(CompactionTriggers.TokensExceed(AppCompactionPolicy.ToolEvictionTokens(inputBudget.Value))));
            if (settings.EnableSummarization)
            {
                var summarizer = await TryResolveSummarizerAsync(config, settings, cancellationToken, chatModelId).ConfigureAwait(false);
                if (summarizer != null)
                {
                    strategies.Add(new SummarizationCompactionStrategy(
                        summarizer,
                        CompactionTriggers.TokensExceed(AppCompactionPolicy.SummarizeTriggerTokens(inputBudget.Value)),
                        target: CompactionTriggers.TokensBelow(AppCompactionPolicy.SummarizeTargetTokens(inputBudget.Value))));
                }
            }

            strategies.Add(new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(AppCompactionPolicy.TruncationTokens(inputBudget.Value))));
        }
        else
        {
            // custom 或模型窗口元数据缺失：按 settings 绝对值运行（缺省值即 legacy 固定阈值，与历史版本行为一致）
            strategies.Add(new ToolResultCompactionStrategy(CompactionTriggers.MessagesExceed(Math.Max(settings.ToolResultTriggerMessages, 1))));
            if (settings.EnableSummarization)
            {
                var summarizer = await TryResolveSummarizerAsync(config, settings, cancellationToken, chatModelId).ConfigureAwait(false);
                if (summarizer != null)
                {
                    strategies.Add(new SummarizationCompactionStrategy(
                        summarizer,
                        CompactionTriggers.TokensExceed(Math.Clamp(settings.SummarizeTriggerTokens, AppCompactionPolicy.MinTokenLimit, AppCompactionPolicy.MaxTokenLimit))));
                }
            }

            strategies.Add(new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(Math.Clamp(settings.HardTokenLimit, AppCompactionPolicy.MinTokenLimit, AppCompactionPolicy.MaxTokenLimit))));
        }

        return new PipelineCompactionStrategy(strategies);
    }

    /// <summary>
    /// 字段级合并压缩参数：应用 execution_settings 显式携带的字段覆盖内置默认值，未携带的字段用内置默认值.
    /// </summary>
    /// <param name="executionSettingsJson">应用执行参数 JSON.</param>
    /// <returns>合并后的压缩参数.</returns>
    private static AppAgentExecutionSettings ResolveSettings(string? executionSettingsJson)
    {
        var settings = new AppAgentExecutionSettings();

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

        if (root["compactionMode"] is JsonNode modeNode && modeNode.GetValueKind() == JsonValueKind.String)
        {
            settings.CompactionMode = modeNode.GetValue<string>();
        }

        if (TryGetInt(root, "toolResultTriggerMessages", out var toolResultTrigger))
        {
            settings.ToolResultTriggerMessages = toolResultTrigger!.Value;
        }

        if (TryGetInt(root, "summarizeTriggerTokens", out var summarizeTrigger))
        {
            settings.SummarizeTriggerTokens = summarizeTrigger!.Value;
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

    /// <summary>
    /// 解析所选对话模型的压缩 input budget（ContextWindow − MaxOutput，与库内 ContextWindowCompactionStrategy 同口径）；
    /// 本轮实际模型（对话端切换后）优先，配置/模型不可用或窗口元数据缺失（≤0）返回 null.
    /// </summary>
    private async Task<int?> TryResolveInputBudgetAsync(AppAgentConfigEntity? config, CancellationToken cancellationToken, Guid? chatModelId)
    {
        var modelId = chatModelId ?? config?.ModelId ?? Guid.Empty;
        if (config == null || modelId == Guid.Empty)
        {
            return null;
        }

        var pair = await _modelResolver.ResolveByIdAsync(modelId, config.TeamId, cancellationToken).ConfigureAwait(false);
        if (pair == null)
        {
            return null;
        }

        var window = pair.Value.Model.ContextWindow;
        if (window <= 0)
        {
            return null;
        }

        var budget = window - Math.Max(pair.Value.Model.MaxOutput, 0);
        return budget > 0 ? budget : window;
    }

    private async Task<IChatClient?> TryResolveSummarizerAsync(AppAgentConfigEntity? config, AppAgentExecutionSettings settings, CancellationToken cancellationToken, Guid? chatModelId)
    {
        if (config == null)
        {
            return null;
        }

        // 未显式配置摘要模型时跟随本轮实际对话模型（对话端切换后摘要与对话保持同一窗口口径）
        var modelId = chatModelId ?? config.ModelId;
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
