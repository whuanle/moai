using System.Text;

namespace MoAI.Database.Schema;

/// <summary>
/// 由 EF Core 生成脚本解析表结构.
/// </summary>
public static class SchemaScriptParser
{
    private static readonly string[] TypeModifiers =
    [
        " NOT NULL",
        " DEFAULT",
        " GENERATED",
        " COLLATE",
        " CHECK",
        " REFERENCES",
        " PRIMARY KEY",
        " UNIQUE",
    ];

    /// <summary>
    /// 解析创建脚本，返回表定义（含索引归属）；忽略注释、序列、种子数据等语句.
    /// </summary>
    /// <param name="createScript">EF Core <c>GenerateCreateScript()</c> 输出.</param>
    /// <returns>表定义列表.</returns>
    public static IReadOnlyList<TableDefinition> Parse(string createScript)
    {
        ArgumentNullException.ThrowIfNull(createScript);

        var statements = SplitStatements(createScript);
        var parsedTables = new Dictionary<string, (string Name, string RawName, string Statement, List<ColumnDefinition> Columns)>(StringComparer.Ordinal);
        var indexesByTable = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var statement in statements)
        {
            if (statement.StartsWith("CREATE TABLE ", StringComparison.Ordinal))
            {
                var parsed = ParseCreateTable(statement);
                if (parsed is null)
                {
                    continue;
                }

                var rawName = parsed.Value.RawName;
                parsedTables[rawName] = parsed.Value;
                order.Add(rawName);
            }
            else if (statement.StartsWith("CREATE INDEX ", StringComparison.Ordinal)
                || statement.StartsWith("CREATE UNIQUE INDEX ", StringComparison.Ordinal))
            {
                var table = ExtractIndexTable(statement);
                if (table is null)
                {
                    continue;
                }

                if (!indexesByTable.TryGetValue(table, out var list))
                {
                    list = [];
                    indexesByTable[table] = list;
                }

                list.Add(statement);
            }
        }

        var result = new List<TableDefinition>(order.Count);
        foreach (var rawName in order)
        {
            var (name, tableRawName, createStatement, columns) = parsedTables[rawName];
            result.Add(new TableDefinition
            {
                Name = name,
                RawName = tableRawName,
                CreateStatement = createStatement,
                Columns = columns,
                IndexStatements = indexesByTable.TryGetValue(rawName, out var indexes) ? indexes : [],
            });
        }

        return result;
    }

    /// <summary>
    /// 把脚本拆成单条语句（按括号深度与分号切分，忽略字符串内的括号）.
    /// </summary>
    /// <param name="script">SQL 脚本.</param>
    /// <returns>语句列表.</returns>
    public static IReadOnlyList<string> SplitStatements(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var statements = new List<string>();
        var builder = new StringBuilder();
        var depth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;

        foreach (var rawLine in script.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (builder.Length == 0 && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            builder.AppendLine(line);

            // 引号状态跨行保持：种子数据里的多行文本字段（技能说明等）会跨越很多行，
            // 若逐行重置，字面量中的括号会把语句切分带偏，导致后面的索引语句被并进前一条
            depth += CountParentheses(line, ref inSingleQuote, ref inDoubleQuote);

            if (depth <= 0 && !inSingleQuote && !inDoubleQuote && line.TrimEnd().EndsWith(';'))
            {
                var statement = builder.ToString().Trim();
                if (statement.Length > 0)
                {
                    statements.Add(statement);
                }

                builder.Clear();
                depth = 0;
                inSingleQuote = false;
                inDoubleQuote = false;
            }
        }

        var rest = builder.ToString().Trim();
        if (rest.Length > 0)
        {
            statements.Add(rest);
        }

        return statements;
    }

    private static (string Name, string RawName, string Statement, List<ColumnDefinition> Columns)? ParseCreateTable(string statement)
    {
        var lines = statement.Split('\n');
        var header = lines[0].TrimEnd('\r');

        // 形如 CREATE TABLE access_app (  /  CREATE TABLE "user" (
        var openIndex = header.IndexOf('(', StringComparison.Ordinal);
        if (openIndex <= 0)
        {
            return null;
        }

        var name = header["CREATE TABLE ".Length..openIndex].Trim();
        var rawName = Unquote(name);
        var columns = new List<ColumnDefinition>();

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith(')'))
            {
                continue;
            }

            line = line.TrimEnd(',').TrimEnd();
            if (line.StartsWith("CONSTRAINT ", StringComparison.Ordinal)
                || line.StartsWith("PRIMARY KEY", StringComparison.Ordinal)
                || line.StartsWith("UNIQUE", StringComparison.Ordinal)
                || line.StartsWith("FOREIGN KEY", StringComparison.Ordinal))
            {
                continue;
            }

            var column = ParseColumn(line);
            if (column is not null)
            {
                columns.Add(column);
            }
        }

        return (name, rawName, statement, columns);
    }

    private static ColumnDefinition? ParseColumn(string line)
    {
        if (line.Length == 0)
        {
            return null;
        }

        string name;
        int cursor;
        if (line[0] == '"')
        {
            var end = line.IndexOf('"', 1);
            if (end < 0)
            {
                return null;
            }

            name = line[..(end + 1)];
            cursor = end + 1;
        }
        else
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space <= 0)
            {
                return null;
            }

            name = line[..space];
            cursor = space;
        }

        var definition = line[cursor..].Trim();
        if (definition.Length == 0)
        {
            return null;
        }

        var modifierIndex = definition.Length;
        foreach (var modifier in TypeModifiers)
        {
            var index = definition.IndexOf(modifier, StringComparison.Ordinal);
            if (index >= 0 && index < modifierIndex)
            {
                modifierIndex = index;
            }
        }

        var typeName = definition[..modifierIndex].Trim();
        int? maxLength = null;
        var openParen = typeName.IndexOf('(', StringComparison.Ordinal);
        if (openParen > 0 && typeName.StartsWith("character varying", StringComparison.Ordinal))
        {
            var closeParen = typeName.IndexOf(')', openParen);
            if (closeParen > openParen && int.TryParse(typeName[(openParen + 1)..closeParen], out var length))
            {
                maxLength = length;
            }
        }

        return new ColumnDefinition
        {
            Name = name,
            RawName = Unquote(name),
            Definition = definition,
            TypeName = typeName,
            HasDefault = definition.Contains(" DEFAULT ", StringComparison.Ordinal),
            IsRequired = definition.Contains("NOT NULL", StringComparison.Ordinal),
            IsIdentity = definition.Contains("GENERATED", StringComparison.Ordinal),
            MaxLength = maxLength,
        };
    }

    private static string? ExtractIndexTable(string statement)
    {
        var onIndex = statement.IndexOf(" ON ", StringComparison.Ordinal);
        if (onIndex < 0)
        {
            return null;
        }

        var rest = statement[(onIndex + 4)..].Trim();
        var cursor = rest.IndexOf(' ', StringComparison.Ordinal);
        var table = cursor > 0 ? rest[..cursor] : rest;
        if (table.EndsWith(" USING", StringComparison.OrdinalIgnoreCase))
        {
            table = table[..table.LastIndexOf(" USING", StringComparison.OrdinalIgnoreCase)];
        }

        return Unquote(table.Trim());
    }

    private static string Unquote(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static int CountParentheses(string line, ref bool inSingleQuote, ref bool inDoubleQuote)
    {
        var depth = 0;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inSingleQuote)
            {
                if (ch == '\'')
                {
                    if (i + 1 < line.Length && line[i + 1] == '\'')
                    {
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }

                continue;
            }

            if (inDoubleQuote)
            {
                if (ch == '"')
                {
                    inDoubleQuote = false;
                }

                continue;
            }

            switch (ch)
            {
                case '\'':
                    inSingleQuote = true;
                    break;
                case '"':
                    inDoubleQuote = true;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                default:
                    break;
            }
        }

        return depth;
    }
}
