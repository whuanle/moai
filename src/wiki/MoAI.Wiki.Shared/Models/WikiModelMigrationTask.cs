namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库向量化模型迁移任务约定：worker_task 登记行 + RabbitMQ 异步重建，队列与绑定类型全链路共用.
/// 与记忆模型迁移同构（worker_task + 临时集合 + 事务换名），以知识库为单位发起.
/// </summary>
public static class WikiModelMigrationTask
{
    /// <summary>
    /// worker_task 绑定类型（BindId 为知识库 id，单库同时只允许一个活跃迁移任务）.
    /// </summary>
    public const string BindType = "wiki-model-migration";

    /// <summary>
    /// RabbitMQ 队列/路由 key（与消息 RouterKey、消费者队列名一致）.
    /// </summary>
    public const string QueueName = "wiki.model.migration";
}
