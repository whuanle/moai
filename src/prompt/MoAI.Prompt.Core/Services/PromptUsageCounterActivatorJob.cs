using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Statistics.Services;

namespace MoAI.Prompt.Services;

/// <summary>
/// 将 Redis 中的提示词使用次数刷新到 PostgreSQL.
/// </summary>
public class PromptUsageCounterActivatorJob : IResourceUsageWriter
{
    private const string CounterName = ResourceUsageNames.Prompt;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptUsageCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public PromptUsageCounterActivatorJob(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(IReadOnlyDictionary<string, long> values)
    {
        var increments = Parse(values);
        if (increments.Count == 0)
        {
            return;
        }

        await using var transaction = await _databaseContext.Database.BeginTransactionAsync();
        foreach (var item in increments)
        {
            await _databaseContext.Prompts
                .Where(x => x.Id == item.Key && x.IsDeleted == 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UseCount, x => x.UseCount + item.Value));
        }

        await transaction.CommitAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult(CounterName);
    }

    /// <summary>
    /// 解析有效的提示词计数增量.
    /// </summary>
    /// <param name="values">Redis 计数快照.</param>
    /// <returns>按提示词 id 索引的增量.</returns>
    internal static IReadOnlyDictionary<int, long> Parse(IReadOnlyDictionary<string, long> values)
    {
        Dictionary<int, long> increments = new();
        foreach (var item in values)
        {
            if (item.Value == 0
                || !int.TryParse(item.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var promptId)
                || promptId <= 0
                || item.Key != promptId.ToString(CultureInfo.InvariantCulture))
            {
                continue;
            }

            increments[promptId] = item.Value;
        }

        return increments;
    }
}
