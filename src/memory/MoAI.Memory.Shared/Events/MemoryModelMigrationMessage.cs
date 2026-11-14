using System;
using Maomi.MQ;
using MoAI.Memory.Models;

namespace MoAI.Memory.Events;

/// <summary>
/// 长期记忆向量化模型迁移任务消息（仅携带任务 id，消费端读 worker_task 取迁移参数）.
/// </summary>
[RouterKey(MemoryMigrationTask.QueueName)]
public class MemoryModelMigrationMessage
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}
