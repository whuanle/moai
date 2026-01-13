using MediatR;

namespace MoAI.Hangfire.Models;

/// <summary>
/// 定时任务实现.
/// </summary>
/// <typeparam name="TParams">触发参数类型</typeparam>
public interface IRecuringJobCommand<TParams> : IRequest<RecuringJobResponse>
{
    /// <summary>
    /// 每次执行都有一个唯一 id.
    /// </summary>
    public Guid TaskId { get; }

    /// <summary>
    /// 任务 key，以此作为增加或取消任务的唯一标识.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Cron 表达式.
    /// </summary>
    public string CronExpression { get; }

    /// <summary>
    /// 触发参数.
    /// </summary>
    public TParams Params { get; }
}
