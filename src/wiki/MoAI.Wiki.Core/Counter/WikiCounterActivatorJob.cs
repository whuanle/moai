using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Hangfire.Services;
using System.Text;

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
        if (values == null || values.Count == 0)
        {
            return;
        }

        // 构建批量更新 SQL
        // 格式: UPDATE [TableName] SET [Counter] = [Counter] + Value WHERE [Id] = IdValue;
        const string UpdateTemplate = "UPDATE {0} SET {1} = {1} + {2} WHERE {3} = {4};";

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

        StringBuilder stringBuilder = new();
        foreach (var item in values)
        {
            if (int.TryParse(item.Key, out int id))
            {
                var sql = string.Format(UpdateTemplate, tableName, counterColumnName, item.Value, idColumnName, id);
                stringBuilder.AppendLine(sql);
            }
        }

        if (stringBuilder.Length > 0)
        {
            await _databaseContext.Database.ExecuteSqlRawAsync(stringBuilder.ToString());
        }
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult("wiki");
    }
}