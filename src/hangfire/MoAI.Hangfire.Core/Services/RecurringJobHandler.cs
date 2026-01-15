#pragma warning disable CA1031 // 不捕获常规异常类型

using Hangfire;
using Hangfire.Common;
using Maomi;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Hangfire.Models;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 定时任务执行器.
/// </summary>
/// <typeparam name="TCommand">命令.</typeparam>
/// <typeparam name="TParams">参数.</typeparam>
[InjectOnScoped]
public class RecurringJobHandler<TCommand, TParams>
    where TCommand : RecuringJobCommand<TParams>, new()
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringJobHandler<TCommand, TParams>> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    /// <param name="mediator"></param>
    public RecurringJobHandler(IServiceProvider serviceProvider, ILogger<RecurringJobHandler<TCommand, TParams>> logger, IMediator mediator)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _mediator = mediator;
    }

    /// <summary>
    /// 执行任务.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cron"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    public async Task HandlerAsync(string key, string cron, TParams @params)
    {
        var recurringJobManager = _serviceProvider.GetRequiredService<IRecurringJobManager>();
        CancellationToken cancellationToken = default;
        try
        {
            var response = await _mediator.Send(
                new TCommand
                {
                    Params = @params,
                    CronExpression = cron,
                    Key = key,
                    TaskId = Guid.NewGuid()
                },
                cancellationToken);

            // 被调用方要求取消任务
            if (response != null && response.IsCancel)
            {
                recurringJobManager.RemoveIfExists(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Task error,Key:{Key}.", key);
        }
    }

    /// <summary>
    /// 执行任务.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="startTime"></param>
    /// <param name="cron"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    public async Task HandlerAsync(string key, DateTimeOffset startTime, string cron, TParams @params)
    {
        var recurringJobManager = _serviceProvider.GetRequiredService<IRecurringJobManager>();
        CancellationToken cancellationToken = default;
        try
        {
            var response = await _mediator.Send(
                new TCommand
                {
                    Params = @params,
                    CronExpression = cron,
                    Key = key,
                    TaskId = Guid.NewGuid()
                },
                cancellationToken);

            // 被调用方要求取消任务
            if (response != null && response.IsCancel)
            {
                recurringJobManager.RemoveIfExists(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Task error,Key:{Key}.", key);
        }

        if (string.IsNullOrEmpty(cron))
        {
            recurringJobManager.RemoveIfExists(key);
        }
        else
        {
            var job = Job.FromExpression<RecurringJobHandler<TCommand, TParams>>(task =>
            task.HandlerAsync(key, startTime, cron, @params));

            // 执行一次完成后，将其转换为定时任务
            recurringJobManager.AddOrUpdate(
                key,
                job,
                cronExpression: cron,
                options: new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });
        }
    }
}