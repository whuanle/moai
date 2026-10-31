using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database;

namespace MoAI.Memory.Services;

/// <summary>
/// Agent 长期记忆向量集合抽象：每个应用一个集合 __agent_mem_{appId}，按需自动建表.
/// </summary>
public interface IMemoryVectorStore
{
    /// <summary>
    /// 确保应用记忆集合存在（不存在则建表与 HNSW 索引）.
    /// </summary>
    Task EnsureCollectionAsync(Guid appId, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量写入/更新记忆记录.
    /// </summary>
    Task UpsertAsync(Guid appId, IReadOnlyList<AgentMemoryVectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定记忆.
    /// </summary>
    Task DeleteAsync(Guid appId, IReadOnlyList<Guid> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向量检索：按用户（与可选类型）过滤.
    /// </summary>
    Task<IReadOnlyList<(AgentMemoryVectorRecord Record, double Score)>> SearchAsync(Guid appId, ReadOnlyMemory<float> queryVector, int top, long userId, IReadOnlyList<int>? types, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列举用户在应用内的全部记忆记录（上限条数保护，调用方自行排序分页）.
    /// </summary>
    Task<IReadOnlyList<AgentMemoryVectorRecord>> ListUserAsync(Guid appId, long userId, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除应用记忆集合（应用删除时清理；集合不存在时静默跳过）.
    /// </summary>
    Task DeleteCollectionAsync(Guid appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保全局记忆集合存在（不存在则建表与 HNSW 索引；维度取全局设置）.
    /// </summary>
    Task EnsureGlobalCollectionAsync(int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量写入/更新全局记忆记录（维度取记录向量长度）.
    /// </summary>
    Task UpsertGlobalAsync(IReadOnlyList<AgentMemoryVectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定全局记忆.
    /// </summary>
    Task DeleteGlobalAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全局记忆向量检索：按用户（与可选类型）过滤.
    /// </summary>
    Task<IReadOnlyList<(AgentMemoryVectorRecord Record, double Score)>> SearchGlobalAsync(ReadOnlyMemory<float> queryVector, int top, long userId, IReadOnlyList<int>? types, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列举用户的全局记忆记录（上限条数保护，调用方自行排序分页）.
    /// </summary>
    Task<IReadOnlyList<AgentMemoryVectorRecord>> ListGlobalUserAsync(long userId, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除全局记忆集合（root 清空全部全局记忆时调用；集合不存在时静默跳过）.
    /// </summary>
    Task DeleteGlobalCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 无用户过滤分页遍历集合全部记忆（迁移重建用；集合不存在或为空返回空列表）.
    /// 应用集合传应用 id，全局集合传 <see cref="MemoryGlobalScope.GlobalAppId"/> 哨兵.
    /// </summary>
    Task<IReadOnlyList<AgentMemoryVectorRecord>> ListAllPageAsync(Guid appId, int skip, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计集合记忆条数（迁移进度口径；集合不存在返回 0）.
    /// 应用集合传应用 id，全局集合传 <see cref="MemoryGlobalScope.GlobalAppId"/> 哨兵.
    /// </summary>
    Task<int> CountCollectionAsync(Guid appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始模型迁移：删除残留临时集合并按目标维度建临时集合（失败可安全重试）.
    /// </summary>
    Task BeginMigrationCollectionAsync(Guid appId, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 迁移写入：将已用新模型重嵌的记录批量写入临时集合.
    /// </summary>
    Task UpsertMigrationCollectionAsync(Guid appId, IReadOnlyList<AgentMemoryVectorRecord> records, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成模型迁移：事务内删除原集合并将临时集合换名为正式集合（换名后新维度生效）.
    /// </summary>
    Task CompleteMigrationCollectionAsync(Guid appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 放弃模型迁移：删除临时集合，原集合保持不动（旧数据完好可重试）.
    /// </summary>
    Task AbortMigrationCollectionAsync(Guid appId, CancellationToken cancellationToken = default);
}
