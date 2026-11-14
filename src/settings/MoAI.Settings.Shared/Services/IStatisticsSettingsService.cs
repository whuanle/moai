using System.Threading;
using System.Threading.Tasks;
using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 资源使用统计设置读取服务（统计粒度）.
/// </summary>
public interface IStatisticsSettingsService
{
    /// <summary>
    /// 读取统计落库粒度，未配置或值非法时回退默认按天.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>统计粒度.</returns>
    Task<ResourceUsageGranularity> GetGranularityAsync(CancellationToken cancellationToken = default);
}
