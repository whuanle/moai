using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
}
