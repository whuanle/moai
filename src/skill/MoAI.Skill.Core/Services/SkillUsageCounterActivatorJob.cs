using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Statistics.Services;

namespace MoAI.Skill.Services;

/// <summary>
/// 将 Redis 中的技能使用次数刷新到 PostgreSQL.
/// </summary>
public class SkillUsageCounterActivatorJob : IResourceUsageWriter
{
    private const string CounterName = ResourceUsageNames.Skill;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillUsageCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public SkillUsageCounterActivatorJob(DatabaseContext databaseContext)
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
            await _databaseContext.Skills
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
    /// 解析有效的技能计数增量.
    /// </summary>
    /// <param name="values">Redis 计数快照.</param>
    /// <returns>按技能 id 索引的增量.</returns>
    internal static IReadOnlyDictionary<Guid, long> Parse(IReadOnlyDictionary<string, long> values)
    {
        Dictionary<Guid, long> increments = new();
        foreach (var item in values)
        {
            if (item.Value == 0
                || !Guid.TryParseExact(item.Key, "N", out var skillId)
                || item.Key != skillId.ToString("N"))
            {
                continue;
            }

            increments[skillId] = item.Value;
        }

        return increments;
    }
}
