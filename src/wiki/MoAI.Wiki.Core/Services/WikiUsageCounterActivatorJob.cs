using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Services;

namespace MoAI.Wiki.Services;

/// <summary>
/// 将 Redis 中的知识库使用次数刷新到 PostgreSQL.
/// </summary>
public class WikiUsageCounterActivatorJob : ICounterActivatorJob
{
    private const string CounterName = "wiki-usage";
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiUsageCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public WikiUsageCounterActivatorJob(DatabaseContext databaseContext)
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
            await _databaseContext.Wikis
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
    /// 解析有效的知识库计数增量.
    /// </summary>
    /// <param name="values">Redis 计数快照.</param>
    /// <returns>按知识库 id 索引的增量.</returns>
    internal static IReadOnlyDictionary<int, long> Parse(IReadOnlyDictionary<string, long> values)
    {
        Dictionary<int, long> increments = new();
        foreach (var item in values)
        {
            if (item.Value <= 0
                || !int.TryParse(item.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var wikiId)
                || wikiId <= 0
                || item.Key != wikiId.ToString(CultureInfo.InvariantCulture))
            {
                continue;
            }

            increments[wikiId] = item.Value;
        }

        return increments;
    }
}