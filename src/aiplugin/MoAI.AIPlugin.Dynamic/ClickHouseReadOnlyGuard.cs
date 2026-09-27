using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// ClickHouse 只读守卫：在通用 PostgreSQL/MySQL 文本守卫（<see cref="SqlReadOnlyGuard"/>）之上补充 ClickHouse 方言特有的拒绝项.
/// </summary>
/// <remarks>
/// 单条语句、只读首关键字（一并拒绝 SYSTEM/ATTACH/EXCHANGE/KILL 等服务器操作语句）与常规写操作/DDL/会话控制
/// 关键字由 <see cref="SqlReadOnlyGuard.Validate"/> 统一处理；
/// 本类补充**外部数据源访问面校验**：以「表函数」形态出现（token 紧跟左括号）的 url/file/s3/remote/cluster 等
/// 才拒绝，避免误伤 <c>SELECT url FROM t</c> 这类把同名标识符当列名用的正常查询。
/// 文本层校验先于连接（与目标库是否可达无关）；HTTP 请求层还会置 <c>readonly=1</c> 作为服务端兜底。
/// </remarks>
internal static class ClickHouseReadOnlyGuard
{
    /// <summary>以表函数形态访问外部数据源或其它库（聚簇/其它数据库）的来源（token 紧跟左括号才判定）.</summary>
    private static readonly HashSet<string> ForbiddenTableFunctions = new(StringComparer.Ordinal)
    {
        "URL",
        "FILE",
        "S3",
        "S3QUEUE",
        "HDFS",
        "AZUREBLOBSTORAGE",
        "REMOTE",
        "REMOTESECURE",
        "CLUSTER",
        "CLUSTERALLREPLICAS",
        "MYSQL",
        "POSTGRESQL",
        "JDBC",
        "ODBC",
        "NATS",
        "ICEBERG",
        "DELTALAKE",
        "HUDI",
    };

    /// <summary>
    /// 校验 ClickHouse SQL 是否为「单条只读语句」.
    /// </summary>
    /// <param name="sql">待校验的 SQL 文本.</param>
    /// <returns>返回 <see langword="null"/> 表示通过；否则返回可直接展示给用户的拒绝理由.</returns>
    public static string? Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return "SQL 不能为空";
        }

        var baseViolation = SqlReadOnlyGuard.Validate(sql);
        if (baseViolation != null)
        {
            return baseViolation;
        }

        var text = SqlReadOnlyGuard.StripLiteralsAndComments(sql);
        var index = 0;
        while (index < text.Length)
        {
            if (!SqlReadOnlyGuard.IsKeywordChar(text[index]))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < text.Length && SqlReadOnlyGuard.IsKeywordChar(text[index]))
            {
                index++;
            }

            var keyword = text.Substring(start, index - start).ToUpperInvariant();

            if (ForbiddenTableFunctions.Contains(keyword) && NextNonSpace(text, index) == '(')
            {
                return $"只允许访问本库数据：不允许通过 {keyword}(...) 表函数访问外部数据源";
            }
        }

        return null;
    }

    /// <summary>
    /// 找到下标之后第一个非空白字符（找不到返回行终止符以外的占位）.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <param name="index">起始下标.</param>
    /// <returns>首个非空白字符；未找到时返回 <see langword="'\0'"/>.</returns>
    private static char NextNonSpace(string text, int index)
    {
        while (index < text.Length)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return text[index];
            }

            index++;
        }

        return '\0';
    }

    /// <summary>
    /// 剥离 ClickHouse 查询末尾的 <c>FORMAT &lt;名称&gt;</c> 子句：出参格式由 HTTP 请求统一指定为 JSONEachRow，
    /// 用户自带的 FORMAT 子句会改变响应形态导致解析失败，故在末尾原样移除（FORMAT 只能作为最后子句出现）.
    /// </summary>
    /// <param name="sql">原始 SQL 文本.</param>
    /// <returns>去除末尾 FORMAT 子句的 SQL.</returns>
    public static string StripTrailingFormatClause(string sql)
    {
        var current = sql.Trim().TrimEnd(';').TrimEnd();
        while (true)
        {
            var position = FindTrailingFormat(current);
            if (position < 0)
            {
                return current;
            }

            current = current.Substring(0, position).TrimEnd();
        }
    }

    /// <summary>
    /// 从尾部找出 <c>FORMAT \&lt;词&gt;</c> 子句（忽略大小写）.
    /// </summary>
    /// <param name="text">SQL 文本.</param>
    /// <returns>FORMAT 关键字起始下标；子句不存在时返回 -1.</returns>
    private static int FindTrailingFormat(string text)
    {
        const string Keyword = "FORMAT";

        var cursor = text.Length;
        while (cursor > 0 && char.IsWhiteSpace(text[cursor - 1]))
        {
            cursor--;
        }

        // 回退格式名（标识符，尾部字母/数字/下划线）
        var formatNameEnd = cursor;
        while (formatNameEnd > 0 && (char.IsLetterOrDigit(text[formatNameEnd - 1]) || text[formatNameEnd - 1] == '_'))
        {
            formatNameEnd--;
        }

        if (formatNameEnd == cursor)
        {
            return -1;
        }

        while (formatNameEnd > 0 && char.IsWhiteSpace(text[formatNameEnd - 1]))
        {
            formatNameEnd--;
        }

        if (formatNameEnd < Keyword.Length
            || string.Compare(text, formatNameEnd - Keyword.Length, Keyword, 0, Keyword.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return -1;
        }

        return formatNameEnd - Keyword.Length;
    }
}
