using System.Text;

namespace MoAI.Database.Schema;

/// <summary>
/// 依据 EF Core 生成脚本构造存量库可执行的幂等 DDL.
/// </summary>
public static class SchemaDdl
{
    private const string CreateTablePrefix = "CREATE TABLE ";
    private const string CreateIndexPrefix = "CREATE INDEX ";
    private const string CreateUniqueIndexPrefix = "CREATE UNIQUE INDEX ";
    private const string CreateSequencePrefix = "CREATE SEQUENCE ";

    /// <summary>
    /// 构造 <c>ALTER TABLE ... ADD COLUMN</c> 用的列定义：缺失列补到非空表时，
    /// 必须给 NOT NULL 列一个兜底默认值，否则 PostgreSQL 会因存量行无值拒绝加列.
    /// </summary>
    /// <param name="column">列定义.</param>
    /// <returns>可直接接在列名后的定义.</returns>
    public static string BuildAddColumnDefinition(ColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (column.HasDefault || column.IsIdentity || !column.IsRequired)
        {
            return column.Definition;
        }

        var fallback = TryGetFallbackDefault(column.TypeName);
        if (fallback is not null)
        {
            // NOT NULL 与 DEFAULT 的书写顺序不影响语义，追加在末尾最省事
            return column.Definition + " DEFAULT " + fallback;
        }

        // 无法推断兜底值（如 vector）：降级为可空列，避免启动直接失败；读侧仍能查询
        return column.Definition.Replace(" NOT NULL", string.Empty, StringComparison.Ordinal).Trim();
    }

    /// <summary>
    /// 把 <c>CREATE TABLE</c> 语句改为幂等形式.
    /// </summary>
    /// <param name="createStatement">建表语句.</param>
    /// <returns>幂等建表语句.</returns>
    public static string MakeTableStatementIdempotent(string createStatement)
    {
        ArgumentNullException.ThrowIfNull(createStatement);

        return createStatement.StartsWith(CreateTablePrefix, StringComparison.Ordinal)
            ? CreateTablePrefix + "IF NOT EXISTS " + createStatement[CreateTablePrefix.Length..]
            : createStatement;
    }

    /// <summary>
    /// 把 <c>CREATE [UNIQUE] INDEX</c> 语句改为幂等形式.
    /// </summary>
    /// <param name="indexStatement">索引语句.</param>
    /// <returns>幂等索引语句.</returns>
    public static string MakeIndexStatementIdempotent(string indexStatement)
    {
        ArgumentNullException.ThrowIfNull(indexStatement);

        if (indexStatement.StartsWith(CreateUniqueIndexPrefix, StringComparison.Ordinal))
        {
            return CreateUniqueIndexPrefix + "IF NOT EXISTS " + indexStatement[CreateUniqueIndexPrefix.Length..];
        }

        if (indexStatement.StartsWith(CreateIndexPrefix, StringComparison.Ordinal))
        {
            return CreateIndexPrefix + "IF NOT EXISTS " + indexStatement[CreateIndexPrefix.Length..];
        }

        return indexStatement;
    }

    /// <summary>
    /// 从生成脚本中提取 <c>CREATE SEQUENCE</c> 语句（改为幂等形式）.
    /// </summary>
    /// <param name="createScript">EF Core 生成脚本.</param>
    /// <returns>幂等序列语句.</returns>
    public static IReadOnlyList<string> ExtractSequenceStatements(string createScript)
    {
        ArgumentNullException.ThrowIfNull(createScript);

        return SchemaScriptParser.SplitStatements(createScript)
            .Where(x => x.StartsWith(CreateSequencePrefix, StringComparison.Ordinal))
            .Select(MakeSequenceStatementIdempotent)
            .ToList();
    }

    /// <summary>
    /// 从生成脚本中提取 <c>CREATE EXTENSION</c> 语句（脚本自身已带 IF NOT EXISTS）.
    /// </summary>
    /// <param name="createScript">EF Core 生成脚本.</param>
    /// <returns>扩展语句.</returns>
    public static IReadOnlyList<string> ExtractExtensionStatements(string createScript)
    {
        ArgumentNullException.ThrowIfNull(createScript);

        return SchemaScriptParser.SplitStatements(createScript)
            .Where(x => x.StartsWith("CREATE EXTENSION ", StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// 把 <c>CREATE SEQUENCE</c> 语句改为幂等形式.
    /// </summary>
    /// <param name="createStatement">序列语句.</param>
    /// <returns>幂等序列语句.</returns>
    public static string MakeSequenceStatementIdempotent(string createStatement)
    {
        ArgumentNullException.ThrowIfNull(createStatement);

        return createStatement.StartsWith(CreateSequencePrefix, StringComparison.Ordinal)
            ? CreateSequencePrefix + "IF NOT EXISTS " + createStatement[CreateSequencePrefix.Length..]
            : createStatement;
    }

    /// <summary>
    /// 根据模型生成「存量库全量补表/补列」的幂等脚本（同时作为人工可执行的 SQL 交付物）.
    /// </summary>
    /// <param name="createScript">EF Core 生成脚本.</param>
    /// <param name="tables">解析后的表定义.</param>
    /// <returns>幂等 DDL 脚本.</returns>
    public static string BuildIdempotentScript(string createScript, IReadOnlyList<TableDefinition> tables)
    {
        ArgumentNullException.ThrowIfNull(createScript);
        ArgumentNullException.ThrowIfNull(tables);

        var builder = new StringBuilder();

        builder.AppendLine("-- 存量库 schema 补齐（由 model 生成，幂等可重复执行）.");
        builder.AppendLine("-- 生成命令：dotnet run --project tool/SchemaSync（禁止手工编辑）.");
        builder.AppendLine("-- 覆盖 EF Core 模型中的全部表与列：建缺失表、补缺失列、建缺失索引；不删除任何表/列.");
        builder.AppendLine();

        foreach (var sequence in ExtractSequenceStatements(createScript))
        {
            builder.AppendLine(sequence);
        }

        builder.AppendLine();

        foreach (var table in tables)
        {
            builder.AppendLine(MakeTableStatementIdempotent(table.CreateStatement));

            foreach (var column in table.Columns)
            {
                builder.Append("ALTER TABLE ")
                    .Append(table.Name)
                    .Append(" ADD COLUMN IF NOT EXISTS ")
                    .Append(column.Name)
                    .Append(' ')
                    .Append(BuildAddColumnDefinition(column))
                    .AppendLine(";");
            }

            builder.AppendLine();
        }

        foreach (var table in tables)
        {
            foreach (var index in table.IndexStatements)
            {
                builder.AppendLine(MakeIndexStatementIdempotent(index));
            }
        }

        return builder.ToString();
    }

    private static string? TryGetFallbackDefault(string typeName)
    {
        if (typeName.StartsWith("character varying", StringComparison.Ordinal)
            || typeName.StartsWith("character(", StringComparison.Ordinal)
            || string.Equals(typeName, "text", StringComparison.Ordinal)
            || string.Equals(typeName, "citext", StringComparison.Ordinal))
        {
            return "''";
        }

        return typeName switch
        {
            "uuid" => "'00000000-0000-0000-0000-000000000000'",
            "jsonb" => "'{}'::jsonb",
            "json" => "'{}'::json",
            "integer" or "int" or "bigint" or "smallint" or "real" or "double precision" => "0",
            "numeric" => "0",
            "boolean" => "false",
            "date" => "CURRENT_DATE",
            "bytea" => "''::bytea",
            _ when typeName.StartsWith("timestamp", StringComparison.Ordinal) => "now()",
            _ when typeName.StartsWith("numeric", StringComparison.Ordinal) => "0",
            _ => null,
        };
    }
}
