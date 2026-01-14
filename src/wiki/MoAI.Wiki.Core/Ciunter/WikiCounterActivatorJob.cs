using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Services;

namespace MoAI.Wiki.Ciunter;

/// <summary>
/// 使用量计数器.
/// </summary>
[InjectOnScoped]
public class WikiCounterActivatorJob : ICounterActivatorJob
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public WikiCounterActivatorJob(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IReadOnlyDictionary<string, int> values)
    {
        var ids = values.Keys.Select(x => int.Parse(x)).ToArray();

        // 刷新这些模型的使用量
        await _databaseContext.Wikis
            .Where(t => ids.Contains(t.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Counter, t => t.Counter + values[t.Id.ToString()]));
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult("wiki");
    }
}