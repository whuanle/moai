using System;
using System.Collections.Generic;
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
/// 应用上下文压缩策略工厂：按 execution_settings 组装「工具结果折叠 → 摘要（可选）→ 滑动窗口 → 紧急截断」管线.
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
    /// <returns>压缩策略.</returns>
    public async Task<CompactionStrategy> BuildAsync(AppAgentConfigEntity? config, CancellationToken cancellationToken = default)
    {
        var settings = AppAgentExecutionSettings.Parse(config?.ExecutionSettings);
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
