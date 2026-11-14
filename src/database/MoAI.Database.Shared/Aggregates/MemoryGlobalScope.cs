namespace MoAI.Database;

/// <summary>
/// 个人全局记忆（跨应用记忆）共享常量：全局记忆向量表固定表名，分片登记以固定哨兵 appId 写入 app_memory_shard（TeamId=0）.
/// </summary>
public static class MemoryGlobalScope
{
    /// <summary>
    /// 全局记忆在 app_memory_shard 的哨兵应用 id（真实应用 id 为随机 Guid，不会冲突）.
    /// </summary>
    public static readonly Guid GlobalAppId = new("6d656d6f-0000-0000-0000-000000000001");

    /// <summary>
    /// 全局记忆向量表名（独立于应用集合 __agent_mem_{appId}）.
    /// </summary>
    public const string CollectionName = "__agent_mem_global";
}
