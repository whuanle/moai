using MediatR;
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
        // 内置默认值与 MoAI.AI.Models.AppAgentExecutionSettings 属性初始值一致（应用未显式配置字段时的运行时合并基线）
        return Task.FromResult(new QueryAgentCompactionDefaultsResponse
        {
            ToolResultTriggerMessages = 16,
            SummarizeTriggerTokens = 16000,
            PreserveTurns = 8,
            HardTokenLimit = 64000,
            EnableSummarization = false,
            SummarizerModelId = string.Empty,
        });
    }
}
