using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Statistics.Services;

/// <summary>
/// 资源使用统计领域服务：各业务模块统一经由本服务把使用增量批量推送到 Redis（原子增/减），
/// 由统计模块后台任务定期批量落库。禁止业务模块自行直写数据库或自建计数.
/// </summary>
public interface IResourceUsageService
{
    /// <summary>
    /// 批量累加一批资源使用增量（原子推送 Redis，支持正负值）.
    /// </summary>
    /// <param name="name">资源类型名（见 <see cref="ResourceUsageNames"/>）.</param>
    /// <param name="counters">key 是资源 id 文本，value 是增量（正增负减，不可为 0）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task RecordAsync(string name, IReadOnlyDictionary<string, long> counters, CancellationToken cancellationToken = default);

    /// <summary>
    /// 累加单个资源使用增量.
    /// </summary>
    /// <param name="name">资源类型名（见 <see cref="ResourceUsageNames"/>）.</param>
    /// <param name="resourceId">资源 id 文本.</param>
    /// <param name="count">增量，默认 1.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task RecordAsync(string name, string resourceId, long count = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// 扣减单个资源使用量（等价于累加负值）.
    /// </summary>
    /// <param name="name">资源类型名（见 <see cref="ResourceUsageNames"/>）.</param>
    /// <param name="resourceId">资源 id 文本.</param>
    /// <param name="count">扣减量，默认 1.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task RemoveAsync(string name, string resourceId, long count = 1, CancellationToken cancellationToken = default);
}
