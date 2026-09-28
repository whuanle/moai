using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MoAI.AI.A2a;

/// <summary>
/// A2A 任务状态机状态（对齐 A2A 协议 TaskStatus.state）.
/// </summary>
public enum AppA2aTaskState
{
    /// <summary>已受理（进入执行前）.</summary>
    Submitted,

    /// <summary>执行中.</summary>
    Working,

    /// <summary>执行完成.</summary>
    Completed,

    /// <summary>执行失败.</summary>
    Failed,

    /// <summary>被取消.</summary>
    Canceled,
}

/// <summary>
/// A2A 任务的进程内记录：状态、会话归属与产出文本（供 <c>tasks/get</c> 查询）；仅尽力而为（进程重启即失）.
/// </summary>
public sealed class AppA2aTaskRecord
{
    /// <summary>
    /// 任务 id（task.id）.
    /// </summary>
    public string TaskId { get; init; } = default!;

    /// <summary>
    /// 上下文 id（task.contextId，承载 MoAI 会话 id）.
    /// </summary>
    public Guid ContextId { get; init; }

    /// <summary>
    /// 归属应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 归属外部用户 id（跨归属不可查询）.
    /// </summary>
    public long ExternalId { get; init; }

    /// <summary>
    /// 当前状态.
    /// </summary>
    public AppA2aTaskState State { get; set; } = AppA2aTaskState.Submitted;

    /// <summary>
    /// 状态附言（失败原因等）.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// 产出文本（按产出顺序累积）.
    /// </summary>
    public List<string> Artifacts { get; } = new();

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// A2A 任务注册表（单例）：按 taskId 登记任务记录与进行中一轮的取消源，
/// 供 <c>tasks/get</c> 查询与 <c>tasks/cancel</c> 中断；进程内尽力而为（重启即失，容量上限自动裁剪最旧完成项）.
/// </summary>
public sealed class AppA2aTaskRegistry
{
    private const int MaxRecords = 1000;

    private readonly ConcurrentDictionary<string, AppA2aTaskRecord> _tasks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记新任务记录.
    /// </summary>
    /// <param name="record">任务记录.</param>
    public void Add(AppA2aTaskRecord record)
    {
        _tasks[record.TaskId] = record;
        if (_tasks.Count <= MaxRecords)
        {
            return;
        }

        foreach (var done in _tasks.Values
            .Where(x => x.State is AppA2aTaskState.Completed or AppA2aTaskState.Failed or AppA2aTaskState.Canceled)
            .OrderBy(x => x.UpdatedTime)
            .Take(_tasks.Count - MaxRecords))
        {
            _tasks.TryRemove(done.TaskId, out _);
        }
    }

    /// <summary>
    /// 按 id 查任务（须同归属外部用户与应用）.
    /// </summary>
    /// <param name="taskId">任务 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="externalId">外部用户 id.</param>
    /// <returns>返回任务记录，不存在或不归属为 null.</returns>
    public AppA2aTaskRecord? Find(string taskId, Guid appId, long externalId)
    {
        return _tasks.TryGetValue(taskId, out var record)
            && record.AppId == appId
            && record.ExternalId == externalId
            ? record
            : null;
    }

    /// <summary>
    /// 登记一轮进行中的运行（取消源）；重复登记返回 false.
    /// </summary>
    /// <param name="taskId">任务 id.</param>
    /// <param name="cancellationTokenSource">取消源.</param>
    /// <returns>是否登记成功.</returns>
    public bool TryRegisterRun(string taskId, CancellationTokenSource cancellationTokenSource)
        => _runs.TryAdd(taskId, cancellationTokenSource);

    /// <summary>
    /// 注销运行登记（仅移除自己登记的取消源）.
    /// </summary>
    /// <param name="taskId">任务 id.</param>
    /// <param name="cancellationTokenSource">取消源.</param>
    public void UnregisterRun(string taskId, CancellationTokenSource cancellationTokenSource)
        => _runs.TryRemove(new KeyValuePair<string, CancellationTokenSource>(taskId, cancellationTokenSource));

    /// <summary>
    /// 取消任务：中断进行中的一轮对话并把状态置为 Canceled；幂等.
    /// </summary>
    /// <param name="taskId">任务 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="externalId">外部用户 id.</param>
    /// <returns>返回任务记录，不存在或不归属为 null.</returns>
    public AppA2aTaskRecord? TryCancel(string taskId, Guid appId, long externalId)
    {
        var record = Find(taskId, appId, externalId);
        if (record == null)
        {
            return null;
        }

        if (_runs.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
        }

        if (record.State is AppA2aTaskState.Submitted or AppA2aTaskState.Working)
        {
            record.State = AppA2aTaskState.Canceled;
            record.UpdatedTime = DateTimeOffset.Now;
        }

        return record;
    }
}
