using MediatR;
using MoAI.AI;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAgentCompactionDefaultsCommand"/>
/// </summary>
public class QueryAgentCompactionDefaultsCommandHandler : IRequestHandler<QueryAgentCompactionDefaultsCommand, QueryAgentCompactionDefaultsResponse>
{
    /// <inheritdoc/>
    public Task<QueryAgentCompactionDefaultsResponse> Handle(QueryAgentCompactionDefaultsCommand request, CancellationToken cancellationToken)
    {
        // 内置默认值与 MoAI.AI.Models.AppAgentExecutionSettings 属性初始值一致（应用未显式配置字段时的运行时合并基线）；
        // 模式与比例常量统一取自 AppCompactionPolicy（运行时 AppCompactionStrategyFactory 同源）
        return Task.FromResult(new QueryAgentCompactionDefaultsResponse
        {
            CompactionMode = AppCompactionPolicy.ModeAuto,
            ToolEvictionRatio = AppCompactionPolicy.ToolEvictionRatio,
            SummarizeTriggerRatio = AppCompactionPolicy.SummarizeTriggerRatio,
            SummarizeTargetRatio = AppCompactionPolicy.SummarizeTargetRatio,
            TruncationRatio = AppCompactionPolicy.TruncationRatio,
            ToolResultTriggerMessages = AppCompactionPolicy.FallbackToolResultTriggerMessages,
            SummarizeTriggerTokens = AppCompactionPolicy.FallbackSummarizeTriggerTokens,
            HardTokenLimit = AppCompactionPolicy.FallbackHardTokenLimit,
            EnableSummarization = false,
            SummarizerModelId = string.Empty,
        });
    }
}
