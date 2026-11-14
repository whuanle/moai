using System;

namespace MoAI.Memory.Queries.Responses;

/// <summary>
/// 长期记忆向量化模型迁移状态响应.
/// </summary>
public class QueryMemoryModelMigrationStatusResponse
{
    /// <summary>
    /// 当前生效向量化模型 id（Guid.Empty 表示尚未配置统一模型）.
    /// </summary>
    public Guid CurrentModelId { get; init; }

    /// <summary>
    /// 当前生效向量化模型名称.
    /// </summary>
    public string CurrentModelName { get; init; } = string.Empty;

    /// <summary>
    /// 当前生效向量维度.
    /// </summary>
    public int CurrentDimensions { get; init; }

    /// <summary>
    /// 平台记忆总条数（应用记忆 + 全局记忆；大于 0 即模型/维度被锁定）.
    /// </summary>
    public int TotalMemoryCount { get; init; }

    /// <summary>
    /// 最近一次迁移任务（活跃任务优先，从未迁移时为 null）.
    /// </summary>
    public MemoryMigrationTaskState? ActiveMigration { get; init; }
}

/// <summary>
/// 迁移任务状态.
/// </summary>
public class MemoryMigrationTaskState
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }

    /// <summary>
    /// 任务状态（2 处理中 / 4 成功 / 5 失败，见 WorkerState）.
    /// </summary>
    public int State { get; init; }

    /// <summary>
    /// 状态消息.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 目标向量化模型 id.
    /// </summary>
    public Guid TargetModelId { get; init; }

    /// <summary>
    /// 目标向量化模型名称.
    /// </summary>
    public string TargetModelName { get; init; } = string.Empty;

    /// <summary>
    /// 目标向量维度.
    /// </summary>
    public int TargetDimensions { get; init; }

    /// <summary>
    /// 计划重建的记忆总条数.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 已重建的记忆条数.
    /// </summary>
    public int MigratedCount { get; init; }

    /// <summary>
    /// 任务创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 任务最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }
}
