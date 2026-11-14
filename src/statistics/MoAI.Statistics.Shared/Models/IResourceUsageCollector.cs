using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Statistics.Services;

/// <summary>
/// 对话轮次使用量收集器（scoped，随请求/任务作用域结束自动批量 flush）：
/// 一轮对话内多次工具/技能/知识库调用先在内存聚合，作用域结束时按资源类型各推送一次 Redis，
/// 避免逐次触发统计命令.
/// </summary>
public interface IResourceUsageCollector
{
    /// <summary>
    /// 是否已收集到增量.
    /// </summary>
    bool HasRecords { get; }

    /// <summary>
    /// 收集一次资源使用.
    /// </summary>
    /// <param name="name">资源类型名（见 <see cref="ResourceUsageNames"/>）.</param>
    /// <param name="resourceId">资源 id 文本.</param>
    /// <param name="count">增量，默认 1.</param>
    void Record(string name, string resourceId, long count = 1);

    /// <summary>
    /// 批量收集一批增量.
    /// </summary>
    /// <param name="name">资源类型名（见 <see cref="ResourceUsageNames"/>）.</param>
    /// <param name="counters">key 是资源 id 文本，value 是增量.</param>
    void RecordBatch(string name, IReadOnlyDictionary<string, long> counters);

    /// <summary>
    /// 把已收集的增量按资源类型批量推送到 Redis（每个资源类型一次）；已 flush 过再调用是空操作.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
