using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Services;
using System;

namespace MoAI.Plugin.Counter;

/// <summary>
/// 使用量计数器.
/// </summary>
[InjectOnScoped]
public class PluginCounterActivatorJob : ICounterActivatorJob
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public PluginCounterActivatorJob(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IReadOnlyDictionary<string, int> values)
    {
        var keys = values.Keys.Where(x => !x.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)).ToArray();
        if (keys.Length > 0)
        {
            // 刷新这些插件的使用量
            await _databaseContext.Plugins
                .Where(t => keys.Contains(t.PluginName))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Counter, t => t.Counter + values[t.PluginName]));
        }

        // 解析 wiki_{id} 出来
        var wiki = values.Where(x => x.Key.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase))
            .ToDictionary(x => int.Parse(x.Key.Remove(0, "wiki_".Length))!, x => x.Value);

        var ids = wiki.Keys.ToArray();

        if (keys.Length > 0)
        {
            // 刷新这些插件的使用量
            await _databaseContext.Wikis
                .Where(t => ids.Contains(t.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Counter, t => t.Counter + wiki[t.Id]));
        }
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult("plugin");
    }
}