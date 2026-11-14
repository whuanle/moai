namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库检索经验聚类约定：MQ 队列名、沉淀条数上限、清理周期等常量.
/// </summary>
public static class WikiRecallClusterDefaults
{
    /// <summary>
    /// RabbitMQ 队列/路由 key（与消息 RouterKey、消费者队列名一致）.
    /// </summary>
    public const string QueueName = "wiki.recall.cluster";

    /// <summary>
    /// 单个聚类沉淀的命中条数上限.
    /// </summary>
    public const int MaxHits = 20;

    /// <summary>
    /// 聚类保留天数：超过该天数未被任何查询命中（last_recall_time 不再更新）的聚类由周期任务清理.
    /// </summary>
    public const int RetentionDays = 30;

    /// <summary>
    /// 清理任务 Hangfire cron（每日 UTC 0 点）.
    /// </summary>
    public const string CleanupCron = "0 0 * * *";

    /// <summary>
    /// 清理任务 key.
    /// </summary>
    public const string CleanupJobKey = "wiki-recall-cluster-cleanup";
}
