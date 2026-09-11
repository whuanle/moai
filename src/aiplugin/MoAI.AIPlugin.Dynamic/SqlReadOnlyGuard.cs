using System;
using System.Collections.Generic;
using System.Text;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// SQL 只读守卫：在把用户提交的 SQL 交给数据库驱动之前做静态校验，拒绝一切可能修改数据的语句.
/// </summary>
/// <remarks>
/// 只读约束分三层，本类是**第一层**（文本层），只负责给出可读的拒绝理由、把明显有害的语句挡在发往数据库之前：
/// <list type="number">
/// <item><description>**文本层（本类）**：剥离注释与字符串/标识符字面量后，要求语句以只读关键字开头、只允许单条语句、且任意位置不得出现写操作/DDL/会话与事务控制关键字。</description></item>
/// <item><description>**连接层**：各 SQL 插件在连接上执行会话级只读设置（PostgreSQL <c>SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY</c>、MySQL <c>SET SESSION TRANSACTION READ ONLY</c>），即使文本校验被绕过，服务端也会拒绝写操作。</description></item>
/// <item><description>**资源层**：行数上限与语句超时，避免超大结果集与长事务。</description></item>
/// </list>
/// 关键字扫描前会剥离注释、字符串字面量、双引号/反引号标识符与 PostgreSQL 美元引用串，因此「字符串或引号标识符里出现关键字」不会误判；
/// 唯一的例外是 MySQL 的**可执行注释** <c>/*! ... */</c>（其内容会被服务端真正执行），其内容会被保留并参与扫描。
/// </remarks>
internal static class SqlReadOnlyGuard
{
    /// <summary>SQL 为空的提示信息.</summary>
    private const string EmptySqlMessage = "SQL 不能为空";

    /// <summary>无法识别语句前缀时的提示信息.</summary>
    private const string UnrecognizedSqlMessage = "无法识别的 SQL 语句";

    /// <summary>允许作为语句首个关键字的只读语句前缀.</summary>
    private static readonly HashSet<string> AllowedLeadingKeywords = new(StringComparer.Ordinal)
    {
        "SELECT",
        "WITH",
        "TABLE",
        "VALUES",
        "SHOW",
        "EXPLAIN",
        "DESCRIBE",
        "DESC",
    };

    /// <summary>
    /// 语句任意位置都不允许出现的关键字：写操作、DDL、权限、导入导出、动态执行、会话与事务控制、库维护命令.
    /// </summary>
    /// <remarks>
    /// 这里只列**不会与普通列名/函数名冲突**的关键字（如 <c>END</c> 因 <c>CASE ... END</c> 被排除），
    /// 避免把合法的只读查询误判；确需使用同名标识符时可用引号包裹（引号标识符不参与关键字扫描）。
    /// </remarks>
    private static readonly HashSet<string> ForbiddenKeywords = new(StringComparer.Ordinal)
    {
        // DML
        "INSERT",
        "UPDATE",
        "DELETE",
        "MERGE",
        "REPLACE",
        "TRUNCATE",

        // DDL
        "DROP",
        "CREATE",
        "ALTER",
        "RENAME",

        // 权限
        "GRANT",
        "REVOKE",

        // 写文件 / 建表 / 赋值
        "INTO",
        "OUTFILE",
        "DUMPFILE",
        "COPY",
        "LOAD",

        // 过程与动态执行
        "CALL",
        "DO",
        "EXECUTE",
        "PREPARE",
        "DEALLOCATE",

        // 会话与事务控制
        "SET",
        "RESET",
        "BEGIN",
        "COMMIT",
        "ROLLBACK",
        "SAVEPOINT",
        "TRANSACTION",

        // 显式锁
        "LOCK",
        "UNLOCK",

        // 库维护命令
        "VACUUM",
        "REINDEX",
        "CLUSTER",
        "REFRESH",
        "ANALYZE",
        "DISCARD",
        "FLUSH",
        "OPTIMIZE",
        "REPAIR",
    };

    /// <summary>
    /// 校验 SQL 是否为「单条只读语句」.
    /// </summary>
    /// <param name="sql">待校验的 SQL 文本.</param>
    /// <returns>返回 <see langword="null"/> 表示通过；否则返回可直接展示给用户的拒绝理由.</returns>
    public static string? Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return EmptySqlMessage;
        }

        var text = StripLiteralsAndComments(sql);

        // 1) 单条语句：只允许末尾出现一个分号
        if (ContainsMultipleStatements(text))
        {
            return "只允许执行单条只读 SQL：不支持一次提交多条语句";
        }

        // 2) 首个关键字必须是只读语句前缀
        var leading = ReadLeadingKeyword(text);
        if (leading == null)
        {
            return UnrecognizedSqlMessage;
        }

        if (!AllowedLeadingKeywords.Contains(leading))
        {
            return $"只允许执行只读 SQL：不支持以 {leading} 开头的语句（仅允许 SELECT、WITH、TABLE、VALUES、SHOW、EXPLAIN、DESCRIBE）";
        }

        // 3) 任意位置不得出现写操作/DDL/会话与事务控制关键字（覆盖 WITH ... DELETE、EXPLAIN ANALYZE <DML>、SELECT ... INTO 等形态）
        foreach (var keyword in ReadKeywords(text))
        {
            if (ForbiddenKeywords.Contains(keyword))
            {
                return $"只允许执行只读 SQL：语句中不允许出现 {keyword}";
            }
        }

        return null;
    }

    /// <summary>
    /// 剥离注释与字面量，只保留参与关键字判断的结构化文本.
    /// </summary>
    /// <param name="sql">原始 SQL 文本.</param>
    /// <returns>剥离后的文本（被剥离的片段替换为空格，不改变其余字符的相对位置）.</returns>
    private static string StripLiteralsAndComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var index = 0;
        while (index < sql.Length)
        {
            var current = sql[index];

            // 行注释：-- ... 直到行尾（MySQL 的 # 注释不做特殊处理，见类注释）
            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = SkipToEndOfLine(sql, index + 2);
                builder.Append(' ');
                continue;
            }

            // 块注释：/* ... */（PostgreSQL 支持嵌套）
            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                var contentStart = index + 2;
                var isExecutable = contentStart < sql.Length && sql[contentStart] == '!';
                index = SkipBlockComment(sql, contentStart, out var contentEnd);

                // MySQL 可执行注释（/*! ... */、/*!50000 ... */）的内容会被服务端执行，必须保留参与扫描
                if (isExecutable && contentEnd > contentStart)
                {
                    builder.Append(sql, contentStart, contentEnd - contentStart);
                }

                builder.Append(' ');
                continue;
            }

            // 单引号字符串（'' 与反斜杠转义）、双引号标识符、反引号标识符
            if (current is '\'' or '"' or '`')
            {
                index = SkipQuoted(sql, index + 1, current);
                builder.Append(' ');
                continue;
            }

            // PostgreSQL 美元引用串：$tag$ ... $tag$
            if (current == '$' && TrySkipDollarQuoted(sql, index, out var dollarEnd))
            {
                index = dollarEnd;
                builder.Append(' ');
                continue;
            }

            builder.Append(current);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// 判断剥离后的文本里是否还存在第二条语句.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <returns>存在多余语句时为 <see langword="true"/>（末尾的单个分号不算）.</returns>
    private static bool ContainsMultipleStatements(string text)
    {
        var index = text.IndexOf(';', StringComparison.Ordinal);
        if (index < 0)
        {
            return false;
        }

        for (var i = index + 1; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 读取语句的首个关键字.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <returns>大写关键字；没有可识别关键字时返回 <see langword="null"/>.</returns>
    private static string? ReadLeadingKeyword(string text)
    {
        foreach (var keyword in ReadKeywords(text))
        {
            return keyword;
        }

        return null;
    }

    /// <summary>
    /// 按词边界切出文本中的所有关键字（统一转大写）.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <returns>关键字序列.</returns>
    private static IEnumerable<string> ReadKeywords(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (!IsKeywordChar(text[index]))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < text.Length && IsKeywordChar(text[index]))
            {
                index++;
            }

            yield return text.Substring(start, index - start).ToUpperInvariant();
        }
    }

    /// <summary>
    /// 判断字符是否属于标识符/关键字.
    /// </summary>
    /// <param name="value">待判断字符.</param>
    /// <returns>字母、数字、下划线或美元符号时为 <see langword="true"/>.</returns>
    private static bool IsKeywordChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '$';
    }

    /// <summary>
    /// 跳到行尾.
    /// </summary>
    /// <param name="sql">SQL 文本.</param>
    /// <param name="index">起始下标.</param>
    /// <returns>行尾下标.</returns>
    private static int SkipToEndOfLine(string sql, int index)
    {
        while (index < sql.Length && sql[index] != '\n')
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// 跳过块注释（支持嵌套）.
    /// </summary>
    /// <param name="sql">SQL 文本.</param>
    /// <param name="index">注释内容起始下标.</param>
    /// <param name="contentEnd">注释内容结束下标（不包含结尾的 <c>*&#47;</c>）.</param>
    /// <returns>注释结束后的下标.</returns>
    private static int SkipBlockComment(string sql, int index, out int contentEnd)
    {
        var depth = 1;
        while (index < sql.Length)
        {
            if (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                depth++;
                index += 2;
                continue;
            }

            if (sql[index] == '*' && index + 1 < sql.Length && sql[index + 1] == '/')
            {
                depth--;
                if (depth == 0)
                {
                    contentEnd = index;
                    return index + 2;
                }

                index += 2;
                continue;
            }

            index++;
        }

        contentEnd = sql.Length;
        return sql.Length;
    }

    /// <summary>
    /// 跳过引号包裹的字面量/标识符.
    /// </summary>
    /// <param name="sql">SQL 文本.</param>
    /// <param name="index">引号之后的下标.</param>
    /// <param name="quote">引号字符.</param>
    /// <returns>闭合引号之后的下标（未闭合时返回文本长度）.</returns>
    private static int SkipQuoted(string sql, int index, char quote)
    {
        while (index < sql.Length)
        {
            var current = sql[index];

            // 反斜杠转义（MySQL 与 PostgreSQL E'' 字符串）
            if (current == '\\' && index + 1 < sql.Length)
            {
                index += 2;
                continue;
            }

            if (current == quote)
            {
                // 双写即转义：'' / "" / ``
                if (index + 1 < sql.Length && sql[index + 1] == quote)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return index;
    }

    /// <summary>
    /// 尝试跳过一个 PostgreSQL 美元引用串.
    /// </summary>
    /// <param name="sql">SQL 文本.</param>
    /// <param name="index">美元符号所在下标.</param>
    /// <param name="end">美元引用串之后的下标.</param>
    /// <returns>形如 <c>$tag$</c> 且能匹配到结束标记时为 <see langword="true"/>.</returns>
    private static bool TrySkipDollarQuoted(string sql, int index, out int end)
    {
        end = index;

        // $1 这类参数占位符不是美元引用串
        var cursor = index + 1;
        if (cursor < sql.Length && char.IsDigit(sql[cursor]))
        {
            return false;
        }

        while (cursor < sql.Length && (char.IsLetterOrDigit(sql[cursor]) || sql[cursor] == '_'))
        {
            cursor++;
        }

        if (cursor >= sql.Length || sql[cursor] != '$')
        {
            return false;
        }

        var tag = sql.Substring(index, cursor - index + 1);
        var close = sql.IndexOf(tag, cursor + 1, StringComparison.Ordinal);
        end = close < 0 ? sql.Length : close + tag.Length;
        return true;
    }
}
