using MediatR;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Settings.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAgentCompactionDefaultsCommand"/>
/// </summary>
public class QueryAgentCompactionDefaultsCommandHandler : IRequestHandler<QueryAgentCompactionDefaultsCommand, QueryAgentCompactionDefaultsResponse>
{
    private readonly IAgentSettingsService _agentSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAgentCompactionDefaultsCommandHandler"/> class.
    /// </summary>
    /// <param name="agentSettingsService">Agent 运行时默认参数读取服务.</param>
    public QueryAgentCompactionDefaultsCommandHandler(IAgentSettingsService agentSettingsService)
    {
        _agentSettingsService = agentSettingsService;
    }

    /// <inheritdoc/>
    public async Task<QueryAgentCompactionDefaultsResponse> Handle(QueryAgentCompactionDefaultsCommand request, CancellationToken cancellationToken)
    {
        var defaults = await _agentSettingsService.GetAgentCompactionDefaultsAsync(cancellationToken);
        return new QueryAgentCompactionDefaultsResponse
        {
            ToolResultTriggerMessages = defaults.ToolResultTriggerMessages,
            SummarizeTriggerTokens = defaults.SummarizeTriggerTokens,
            PreserveTurns = defaults.PreserveTurns,
            HardTokenLimit = defaults.HardTokenLimit,
            EnableSummarization = defaults.EnableSummarization,
            SummarizerModelId = defaults.SummarizerModelId ?? string.Empty,
        };
    }
}
