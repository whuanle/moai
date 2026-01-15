using MediatR;
using MoAI.Hangfire.Services;

namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// <inheritdoc cref="QueryRecurringJobCommand"/>
/// </summary>
public class QueryRecurringJobCommandHandler : IRequestHandler<QueryRecurringJobCommand, QueryRecurringJobCommandResponse>
{
    private readonly IRecurringJobService _recurringJobService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRecurringJobCommandHandler"/> class.
    /// </summary>
    /// <param name="recurringJobService">定时任务服务.</param>
    public QueryRecurringJobCommandHandler(IRecurringJobService recurringJobService)
    {
        _recurringJobService = recurringJobService;
    }

    /// <inheritdoc/>
    public async Task<QueryRecurringJobCommandResponse> Handle(QueryRecurringJobCommand request, CancellationToken cancellationToken)
    {
        var key = $"wiki_plugin_{request.WikiId}_{request.ConfigId}";
        var jobInfo = await _recurringJobService.QueryJobAsync(key);

        return new QueryRecurringJobCommandResponse
        {
            Cron = jobInfo.Cron,
            Error = jobInfo.Error,
            IsExist = jobInfo.IsExist,
            LastExecution = jobInfo.LastExecution,
            LastJobState = jobInfo.LastJobState,
            LoadException = jobInfo.LoadException,
            NextExecution = jobInfo.NextExecution
        };
    }
}