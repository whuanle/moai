namespace MoAI.Hangfire.Models;

/// <summary>
/// 定时任务触发接口，这个命令会被定时任务触发.
/// </summary>
/// <typeparam name="TParams">触发参数类型</typeparam>
public abstract class RecuringJobCommand<TParams> : IRecuringJobCommand<TParams>
{
    /// <inheritdoc/>
    public virtual Guid TaskId { get; init; }

    /// <inheritdoc/>
    public virtual string Key { get; init; } = string.Empty!;

    /// <inheritdoc/>
    public virtual string CronExpression { get; init; } = string.Empty!;

    /// <inheritdoc/>
    public virtual TParams Params { get; init; } = default!;
}
