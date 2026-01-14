using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Services;

namespace MoAI.AiModel.Counter;

/// <summary>
/// 使用量计数器.
/// </summary>
[InjectOnScoped]
public class AiCounterActivatorJob : ICounterActivatorJob
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AiCounterActivatorJob(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IReadOnlyDictionary<string, int> values)
    {
        var kvs = values.ToDictionary(x => int.Parse(x.Key), x => x.Value);
        var ids = kvs.Keys.ToArray();

        // todo: 后续优化

        //// 刷新这些模型的使用量
        //await _databaseContext.AiModels
        //    .Where(t => ids.Contains(t.Id))
        //    .ExecuteUpdateAsync(setters => setters
        //        .SetProperty(t => t.Counter, t => t.Counter + kvs[t.Id]));

        var enntities = await _databaseContext.AiModels.Where(t => ids.Contains(t.Id)).ToArrayAsync();
        foreach (var item in enntities)
        {
            item.Counter += kvs[item.Id];
        }

        await _databaseContext.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult("aimodel");
    }
}