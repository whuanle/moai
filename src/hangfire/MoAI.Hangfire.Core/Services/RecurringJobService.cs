using Hangfire;
using Maomi;
using Microsoft.Extensions.Logging;
using MoAI.Hangfire.Models;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 定时任务处理.
/// </summary>
[InjectOnScoped]
public class RecurringJobService : IRecurringJobService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<RecurringJobService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobService"/> class.
    /// </summary>
    /// <param name="recurringJobManager"></param>
    /// <param name="logger"></param>
    public RecurringJobService(IRecurringJobManager recurringJobManager, ILogger<RecurringJobService> logger)
    {
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, string cron, TParams @params)
        where TCommand : RecuringJobCommand<TParams>, new()
    {
        await Task.CompletedTask;
        _recurringJobManager.AddOrUpdate<RecurringJobHandler>(
            key,
            task => task.HandlerAsync<TCommand, TParams>(key, cron, @params),
            cronExpression: cron,
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, DateTimeOffset startTime, string cron, TParams @params)
                where TCommand : RecuringJobCommand<TParams>, new()
    {
        await Task.CompletedTask;

        var cronExpression = $"{startTime.Minute} {startTime.Hour} {startTime.Day} {startTime.Month} * {startTime.Year}";

        _recurringJobManager.AddOrUpdate<RecurringJobHandler>(
            key,
            task => task.HandlerAsync<TCommand, TParams>(key, startTime, cron, @params),
            cronExpression: cronExpression,
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, DateTimeOffset startTime, TParams @params)
                where TCommand : RecuringJobCommand<TParams>, new()
    {
        await Task.CompletedTask;

        var cronExpression = $"{startTime.Minute} {startTime.Hour} {startTime.Day} {startTime.Month} * {startTime.Year}";

        _recurringJobManager.AddOrUpdate<RecurringJobHandler>(
            key,
            task => task.HandlerAsync<TCommand, TParams>(key, startTime, string.Empty, @params),
            cronExpression: cronExpression,
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    /// <inheritdoc/>
    public async Task RemoveRecurringJobAsync(string key)
    {
        await Task.CompletedTask;
        _recurringJobManager.RemoveIfExists(key);
    }
}
