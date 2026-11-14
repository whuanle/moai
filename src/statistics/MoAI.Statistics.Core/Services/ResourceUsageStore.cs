using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;

namespace MoAI.Statistics.Services;

/// <summary>
/// 统一统计表落库：resource_usage_daily（按日/按小时桶）与 resource_usage_total（总量），均以 upsert 累加.
/// </summary>
public class ResourceUsageStore
{
    /// <summary>
    /// usage_hour 的按天粒度哨兵值.
    /// </summary>
    public const int WholeDayHour = -1;

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceUsageStore"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ResourceUsageStore(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 把一批计数增量累加到统一统计表：日表归属 <paramref name="usageDate"/> + <paramref name="usageHour"/> 桶，总量表直接累加.
    /// </summary>
    /// <param name="usageDate">统计日期.</param>
    /// <param name="usageHour">统计小时（0~23），<see cref="WholeDayHour"/> 表示按天粒度.</param>
    /// <param name="name">资源类型名.</param>
    /// <param name="values">key 是资源 id 文本，value 是增量（正增负减）.</param>
    /// <returns>异步任务.</returns>
    public async Task RecordAsync(DateOnly usageDate, int usageHour, string name, IReadOnlyDictionary<string, long> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        await using var transaction = await _databaseContext.Database.BeginTransactionAsync();
        foreach (var item in values)
        {
            if (item.Value == 0 || string.IsNullOrEmpty(item.Key))
            {
                continue;
            }

            await _databaseContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO resource_usage_daily (usage_date, usage_hour, name, resource_id, count, create_time, update_time)
                VALUES ({usageDate}, {usageHour}, {name}, {item.Key}, {item.Value}, timezone('utc', now()), timezone('utc', now()))
                ON CONFLICT (usage_date, usage_hour, name, resource_id)
                DO UPDATE SET count = resource_usage_daily.count + EXCLUDED.count,
                              update_time = timezone('utc', now());
                """);
            await _databaseContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO resource_usage_total (name, resource_id, total, create_time, update_time)
                VALUES ({name}, {item.Key}, {item.Value}, timezone('utc', now()), timezone('utc', now()))
                ON CONFLICT (name, resource_id)
                DO UPDATE SET total = resource_usage_total.total + EXCLUDED.total,
                              update_time = timezone('utc', now());
                """);
        }

        await transaction.CommitAsync();
    }
}
