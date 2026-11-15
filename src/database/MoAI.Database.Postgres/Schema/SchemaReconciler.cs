using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MoAI.Database.Schema;

/// <summary>
/// 存量库 schema 自动补全：<c>EnsureCreated</c> 只在建库时建表，不会给已有库补表/补列，
/// 而手写的幂等 DDL 清单总会滞后于模型，导致升级部署后实体查询报 42P01/42703（表现为接口 500）。
/// 这里改为每次启动用 EF Core 模型与库内 information_schema 做差集，只对真正缺失的表/列/索引下 DDL，
/// 保证任何存量库都能就地补齐到当前模型（只增不删）.
/// </summary>
public static class SchemaReconciler
{
    private const char FieldSeparator = '\u001f';

    /// <summary>
    /// 对当前数据库执行一次补全.
    /// </summary>
    /// <param name="databaseContext">数据库上下文（模型来源）.</param>
    /// <param name="logger">日志.</param>
    /// <param name="dryRun">为 true 时只做差集比对并返回计划，不执行任何 DDL（运维预检用）.</param>
    /// <returns>补全结果.</returns>
    public static SchemaSyncResult Apply(DbContext databaseContext, ILogger logger, bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(databaseContext);
        ArgumentNullException.ThrowIfNull(logger);

        var createScript = databaseContext.Database.GenerateCreateScript();
        var modelTables = SchemaScriptParser.Parse(createScript);
        var result = new SchemaSyncResult();

        var connection = databaseContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            var existingTables = new HashSet<string>(QueryValues(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() AND table_type = 'BASE TABLE'"), StringComparer.Ordinal);
            var existingColumns = QueryColumns(connection);
            var existingIndexes = new HashSet<string>(QueryValues(connection, "SELECT indexname FROM pg_indexes WHERE schemaname = current_schema()"), StringComparer.Ordinal);

            foreach (var sequence in SchemaDdl.ExtractSequenceStatements(createScript))
            {
                if (!dryRun)
                {
                    Execute(connection, sequence);
                }
            }

            // 扩展建库脚本会带，存量库一般已装；失败只告警（非超级用户可能无权限）
            foreach (var extension in SchemaDdl.ExtractExtensionStatements(createScript))
            {
                if (dryRun)
                {
                    continue;
                }

                try
                {
                    Execute(connection, extension);
                }
                catch (DbException ex)
                {
                    result.Warnings.Add($"创建扩展失败：{ex.Message}");
                    logger.LogWarning(ex, "Schema sync: create extension failed: {Statement}", extension);
                }
            }

            var failures = new List<string>();

            foreach (var table in modelTables)
            {
                if (!existingTables.Contains(table.RawName))
                {
                    if (!dryRun && !ExecuteOrFail(connection, table.CreateStatement, $"建表 {table.RawName}", failures))
                    {
                        continue;
                    }

                    existingTables.Add(table.RawName);
                    existingColumns[table.RawName] = table.Columns.ToDictionary(x => x.RawName, x => new ColumnState(x.TypeName, x.MaxLength), StringComparer.Ordinal);
                    result.CreatedTables.Add(table.RawName);
                    continue;
                }

                if (!existingColumns.TryGetValue(table.RawName, out var columns))
                {
                    columns = new Dictionary<string, ColumnState>(StringComparer.Ordinal);
                    existingColumns[table.RawName] = columns;
                }

                foreach (var column in table.Columns)
                {
                    if (!columns.TryGetValue(column.RawName, out var state))
                    {
                        var statement = new StringBuilder()
                            .Append("ALTER TABLE ").Append(table.Name)
                            .Append(" ADD COLUMN IF NOT EXISTS ").Append(column.Name).Append(' ')
                            .Append(SchemaDdl.BuildAddColumnDefinition(column))
                            .Append(';')
                            .ToString();

                        var added = dryRun || ExecuteOrFail(connection, statement, $"补列 {table.RawName}.{column.RawName}", failures);
                        if (added)
                        {
                            result.AddedColumns.Add($"{table.RawName}.{column.RawName}");
                            columns[column.RawName] = new ColumnState(column.TypeName, column.MaxLength);
                        }

                        continue;
                    }

                    // 只处理「同类型加宽」这一种安全变更（如 varchar(20) -> varchar(50)），其余类型差异不动
                    if (column.MaxLength is > 0 && state.MaxLength is > 0 && state.MaxLength < column.MaxLength)
                    {
                        var statement = new StringBuilder()
                            .Append("ALTER TABLE ").Append(table.Name)
                            .Append(" ALTER COLUMN ").Append(column.Name)
                            .Append(" TYPE character varying(").Append(column.MaxLength.Value.ToString(CultureInfo.InvariantCulture)).Append(");")
                            .ToString();

                        try
                        {
                            if (!dryRun)
                            {
                                Execute(connection, statement);
                            }

                            result.WidenedColumns.Add($"{table.RawName}.{column.RawName}");
                            columns[column.RawName] = new ColumnState(column.TypeName, column.MaxLength);
                        }
                        catch (DbException ex)
                        {
                            result.Warnings.Add($"加宽列 {table.RawName}.{column.RawName} 失败：{ex.Message}");
                            logger.LogWarning(ex, "Schema sync: widen column {Table}.{Column} failed.", table.RawName, column.RawName);
                        }
                    }
                }
            }

            foreach (var table in modelTables)
            {
                foreach (var indexStatement in table.IndexStatements)
                {
                    var indexName = GetIndexName(indexStatement);
                    if (indexName is null || existingIndexes.Contains(indexName))
                    {
                        continue;
                    }

                    try
                    {
                        if (!dryRun)
                        {
                            Execute(connection, SchemaDdl.MakeIndexStatementIdempotent(indexStatement));
                        }

                        existingIndexes.Add(indexName);
                        result.CreatedIndexes.Add(indexName);
                    }
                    catch (DbException ex)
                    {
                        // 唯一索引可能因存量脏数据建不起来：记录并继续，绝不因此阻断启动
                        result.Warnings.Add($"建索引 {indexName} 失败：{ex.Message}");
                        logger.LogWarning(ex, "Schema sync: create index {Index} failed.", indexName);
                    }
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("存量库 schema 自动补全失败：" + string.Join("；", failures));
            }
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }

        if (result.HasChanges)
        {
            logger.LogInformation(
                "Schema sync applied: {TableCount} table(s), {ColumnCount} column(s), {WidenedCount} widened, {IndexCount} index(es).",
                result.CreatedTables.Count,
                result.AddedColumns.Count,
                result.WidenedColumns.Count,
                result.CreatedIndexes.Count);
        }

        foreach (var warning in result.Warnings)
        {
            logger.LogWarning("Schema sync warning: {Warning}", warning);
        }

        return result;
    }

    private static bool IsFailed(IReadOnlyList<string> failures, string tableName)
    {
        return failures.Any(x => x.StartsWith("建表 " + tableName, StringComparison.Ordinal));
    }

    private static bool ExecuteOrFail(DbConnection connection, string statement, string description, List<string> failures)
    {
        try
        {
            Execute(connection, statement);
            return true;
        }
        catch (DbException ex)
        {
            failures.Add($"{description}：{ex.Message}");
            return false;
        }
    }

    private static void Execute(DbConnection connection, string statement)
    {
        using var command = connection.CreateCommand();
        command.CommandText = statement;
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }

    private static List<string> QueryValues(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var values = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values;
    }

    private static Dictionary<string, Dictionary<string, ColumnState>> QueryColumns(DbConnection connection)
    {
        const string sql = """
            SELECT table_name || chr(31) || column_name || chr(31) || data_type || chr(31) || coalesce(character_maximum_length, -1)::text
            FROM information_schema.columns
            WHERE table_schema = current_schema()
            """;

        var result = new Dictionary<string, Dictionary<string, ColumnState>>(StringComparer.Ordinal);
        foreach (var row in QueryValues(connection, sql))
        {
            var parts = row.Split(FieldSeparator);
            if (parts.Length < 4)
            {
                continue;
            }

            if (!result.TryGetValue(parts[0], out var columns))
            {
                columns = new Dictionary<string, ColumnState>(StringComparer.Ordinal);
                result[parts[0]] = columns;
            }

            var maxLength = int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) && length > 0
                ? length
                : (int?)null;

            columns[parts[1]] = new ColumnState(parts[2], maxLength);
        }

        return result;
    }

    private static string? GetIndexName(string indexStatement)
    {
        var tokens = indexStatement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var indexKeyword = Array.FindIndex(tokens, x => string.Equals(x, "INDEX", StringComparison.OrdinalIgnoreCase));
        return indexKeyword >= 0 && indexKeyword + 1 < tokens.Length
            ? tokens[indexKeyword + 1].Trim('"')
            : null;
    }

    private sealed record ColumnState(string TypeName, int? MaxLength);
}
