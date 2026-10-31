namespace MoAI.Memory.Models;

/// <summary>
/// 长期记忆向量化模型迁移任务约定：worker_task 登记行 + RabbitMQ 异步重建，队列与绑定类型全链路共用.
/// </summary>
public static class MemoryMigrationTask
{
    /// <summary>
    /// worker_task 绑定类型（BindId 固定 0，全平台同时只允许一个活跃迁移任务）.
    /// </summary>
    public const string BindType = "memory-migration";

    /// <summary>
    /// RabbitMQ 队列/路由 key（与消息 RouterKey、消费者队列名一致）.
    /// </summary>
    public const string QueueName = "memory.model.migration";
}
