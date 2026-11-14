namespace MoAI.Statistics.Services;

/// <summary>
/// 资源使用统计写入器：统计模块定期批量从 Redis 取出计数后，按 name 分发给对应写入器，
/// 写入器自己把数量写入资源所属的数据库表（如 plugin.counter、app.use_count）.
/// </summary>
public interface IResourceUsageWriter
{
    /// <summary>
    /// 获取该写入器负责的资源类型名（与 <see cref="ResourceUsageNames"/> 对应）.
    /// </summary>
    /// <returns>资源类型名.</returns>
    Task<string> GetNameAsync();

    /// <summary>
    /// 批量写入一批计数增量，要做事务，失败时应抛出异常，统计模块会把增量回补 Redis 下次重试.
    /// </summary>
    /// <param name="values">key 是资源 id 文本，value 是增量.</param>
    /// <returns>异步任务.</returns>
    Task WriteAsync(IReadOnlyDictionary<string, long> values);
}
