using DocumentFormat.OpenXml.Office2010.Excel;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Hangfire.Services;
using System.Text;

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
    public Task<string> GetNameAsync()
    {
        return Task.FromResult("plugin");
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IReadOnlyDictionary<string, int> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        // 构建批量更新 SQL
        // 格式: UPDATE [TableName] SET [Counter] = [Counter] + Value WHERE [Id] = IdValue;
        const string UpdateTemplate = "UPDATE {0} SET {1} = {1} + {2} WHERE {3} = {4};";
        StringBuilder stringBuilder = new();

        UpdatePluginAsync(values, UpdateTemplate, stringBuilder);
        UpdateWikiPluginAsync(values, UpdateTemplate, stringBuilder);

        if (stringBuilder.Length > 0)
        {
            await _databaseContext.Database.ExecuteSqlRawAsync(stringBuilder.ToString());
        }
    }

    private void UpdatePluginAsync(IReadOnlyDictionary<string, int> values, string UpdateTemplate, StringBuilder stringBuilder)
    {
        var entityType = _databaseContext.Model.FindEntityType(typeof(PluginEntity));
        if (entityType == null)
        {
            return;
        }

        // 获取表名和Schema
        var tableName = entityType.GetTableName();
        var schema = entityType.GetSchema();

        // 获取列名映射
        var keyProperty = entityType.FindProperty(nameof(PluginEntity.PluginName));
        var keyColumnName = keyProperty?.GetColumnName();

        var counterProperty = entityType.FindProperty(nameof(PluginEntity.Counter));
        var counterColumnName = counterProperty?.GetColumnName();

        if (keyColumnName == null || counterColumnName == null)
        {
            return;
        }

        foreach (var item in values.Where(x => !x.Key.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)))
        {
            var sql = string.Format(UpdateTemplate, tableName, counterColumnName, item.Value, keyColumnName, $"'{item.Key}'");
            stringBuilder.AppendLine(sql);
        }

        return;
    }

    private void UpdateWikiPluginAsync(IReadOnlyDictionary<string, int> values, string UpdateTemplate, StringBuilder stringBuilder)
    {
        var entityType = _databaseContext.Model.FindEntityType(typeof(WikiEntity));
        if (entityType == null)
        {
            return;
        }

        // 获取表名和Schema
        var tableName = entityType.GetTableName();
        var schema = entityType.GetSchema();

        // 获取列名映射
        var idProperty = entityType.FindProperty(nameof(WikiEntity.Id));
        var idColumnName = idProperty?.GetColumnName();

        var counterProperty = entityType.FindProperty(nameof(WikiEntity.Counter));
        var counterColumnName = counterProperty?.GetColumnName();

        if (idColumnName == null || counterColumnName == null)
        {
            return;
        }

        foreach (var item in values.Where(x => x.Key.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)))
        {
            // wiki_{id}
            if (int.TryParse(item.Key.Remove(0, "wiki_".Length), out int id))
            {
                var sql = string.Format(UpdateTemplate, tableName, counterColumnName, item.Value, idColumnName, id);
                stringBuilder.AppendLine(sql);
            }
        }

        return;
    }
}