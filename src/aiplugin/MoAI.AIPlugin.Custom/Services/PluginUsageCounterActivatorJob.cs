using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Services;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 将 Redis 中的插件使用次数刷新到 PostgreSQL.
/// </summary>
public class PluginUsageCounterActivatorJob : ICounterActivatorJob
{
    private const string CounterName = "plugin-usage";
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUsageCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public PluginUsageCounterActivatorJob(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IReadOnlyDictionary<string, long> values)
    {
        var increments = Parse(values);
        if (increments.Count == 0)
        {
            return;
        }

        await using var transaction = await _databaseContext.Database.BeginTransactionAsync();
        foreach (var item in increments)
        {
            await _databaseContext.Plugins
                .Where(x => x.Id == item.Key && x.IsDeleted == 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Counter, x => x.Counter + item.Value));
        }

        await transaction.CommitAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult(CounterName);
    }

    /// <summary>
    /// 解析有效的插件计数增量.
    /// </summary>
    /// <param name="values">Redis 计数快照.</param>
    /// <returns>按插件 id 索引的增量.</returns>
    internal static IReadOnlyDictionary<Guid, long> Parse(IReadOnlyDictionary<string, long> values)
    {
        Dictionary<Guid, long> increments = new();
        foreach (var item in values)
        {
            if (item.Value <= 0
                || !Guid.TryParseExact(item.Key, "N", out var pluginId)
                || item.Key != pluginId.ToString("N"))
            {
                continue;
            }

            increments[pluginId] = item.Value;
        }

        return increments;
    }
}