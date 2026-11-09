using System;
using Maomi.MQ;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库向量化模型迁移任务消息（仅携带任务 id，消费端读 worker_task 取迁移参数）.
/// </summary>
[RouterKey(WikiModelMigrationTask.QueueName)]
public class WikiModelMigrationMessage
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }
}
