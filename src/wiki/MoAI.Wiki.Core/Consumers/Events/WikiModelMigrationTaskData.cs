using System;
using System.Text.Json;

namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库向量化模型迁移任务数据（序列化进 worker_task.data，进度随迁移回写）.
/// </summary>
public class WikiModelMigrationTaskData
{
    /// <summary>
    /// 序列化选项（发起端与消费端统一 Web 口径）.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 目标向量化模型 id.
    /// </summary>
    public Guid TargetModelId { get; init; }

    /// <summary>
    /// 目标向量维度.
    /// </summary>
    public int TargetDimensions { get; init; }

    /// <summary>
    /// 迁移前生效的向量化模型 id.
    /// </summary>
    public Guid SourceModelId { get; init; }

    /// <summary>
    /// 迁移前生效的向量维度.
    /// </summary>
    public int SourceDimensions { get; init; }

    /// <summary>
    /// 计划重建的向量总条数（消费端按实际向量数回填）.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 已重建的向量条数（进度回写）.
    /// </summary>
    public int MigratedCount { get; set; }
}
