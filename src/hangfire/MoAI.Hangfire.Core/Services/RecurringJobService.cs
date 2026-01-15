using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Maomi;
using Microsoft.Extensions.Logging;
using MoAI.Hangfire.Models;
using MoAI.Infra.Extensions;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 定时任务处理.
/// </summary>
[InjectOnScoped]
public class RecurringJobService : IRecurringJobService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly JobStorage _jobStorage;
    private readonly ILogger<RecurringJobService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobService"/> class.
    /// </summary>
    /// <param name="recurringJobManager"></param>
    /// <param name="logger"></param>
    /// <param name="jobStorage"></param>
    public RecurringJobService(IRecurringJobManager recurringJobManager, ILogger<RecurringJobService> logger, JobStorage jobStorage)
    {
        _recurringJobManager = recurringJobManager;
        _logger = logger;
        _jobStorage = jobStorage;
    }

    /// <inheritdoc/>
    public async Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, string cron, TParams @params)
        where TCommand : RecuringJobCommand<TParams>, new()
    {
        await Task.CompletedTask;

        var job = Job.FromExpression<RecurringJobHandler<TCommand, TParams>>(task =>
            task.HandlerAsync(key, cron, @params));

        _recurringJobManager.AddOrUpdate(
            key,
            job,
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

        var job = Job.FromExpression<RecurringJobHandler<TCommand, TParams>>(task =>
            task.HandlerAsync(key, startTime, cron, @params));

        _recurringJobManager.AddOrUpdate(
            key,
            job,
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

        var job = Job.FromExpression<RecurringJobHandler<TCommand, TParams>>(task =>
            task.HandlerAsync(key, startTime, string.Empty, @params));

        _recurringJobManager.AddOrUpdate(
            key,
            job,
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

    /// <summary>
    /// 查询某个 key 有没有任务.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<JobInfo> QueryJobAsync(string key)
    {
        using var connection = _jobStorage.GetConnection();
        var job = connection.GetRecurringJobs(new[] { key }).FirstOrDefault();
        return new JobInfo
        {
            LastExecution = job?.LastExecution,
            NextExecution = job?.NextExecution,
            Cron = job?.Cron,
            Error = job?.Error,
            LastJobState = job?.LastJobState,
            IsExist = !(job?.Removed ?? true),
            LoadException = job?.LoadException?.Message,
        };
    }
}
