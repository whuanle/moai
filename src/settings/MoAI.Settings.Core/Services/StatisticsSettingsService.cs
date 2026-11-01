using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 资源使用统计设置读取服务默认实现.
/// </summary>
public class StatisticsSettingsService : IStatisticsSettingsService
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatisticsSettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public StatisticsSettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<ResourceUsageGranularity> GetGranularityAsync(CancellationToken cancellationToken = default)
    {
        var value = await _databaseContext.Settings
            .AsNoTracking()
            .Where(s => s.Key == SettingDefinitions.StatisticsGranularityKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return string.Equals(value, "hour", System.StringComparison.OrdinalIgnoreCase)
            ? ResourceUsageGranularity.Hour
            : ResourceUsageGranularity.Day;
    }
}
