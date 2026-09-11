using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 把 ADO.NET 的 <see cref="DbDataReader"/> 读成「列名 + 行字典」结构，供 PostgreSQL / MySQL 两个只读查询插件复用.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>**行数上限**：最多读 <c>maxRows</c> 行，用于判断是否还有更多行时会多读一行后立即停止（不把整个结果集拉进内存）。</description></item>
/// <item><description>**重名列**：结果集允许出现同名列（如 <c>SELECT a.id, b.id</c>），字典键会自动追加 <c>_2</c>、<c>_3</c> 后缀避免相互覆盖。</description></item>
/// <item><description>**取值归一**：<see cref="DBNull"/> 归一为 <see langword="null"/>；二进制列（blob/bytea）转 Base64 文本，避免序列化出巨大的整型数组。</description></item>
/// </list>
/// </remarks>
internal static class SqlResultReader
{
    /// <summary>重名列的后缀分隔符.</summary>
    private const string DuplicateSuffixSeparator = "_";

    /// <summary>
    /// 读取结果集.
    /// </summary>
    /// <param name="reader">已执行查询的读取器（调用方负责释放）.</param>
    /// <param name="maxRows">最多返回的行数（&gt;0）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>查询结果的中立表示.</returns>
    public static async Task<SqlQueryResult> ReadAsync(DbDataReader reader, int maxRows, CancellationToken cancellationToken)
    {
        var columns = new List<string>(reader.FieldCount);
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(EnsureUniqueName(reader.GetName(i), used));
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var truncated = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }

            var row = new Dictionary<string, object?>(columns.Count, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false) ? null : reader.GetValue(i);
                row[columns[i]] = Normalize(value);
            }

            rows.Add(row);
        }

        return new SqlQueryResult(columns, rows, truncated);
    }

    /// <summary>
    /// 为列名生成不重复的键.
    /// </summary>
    /// <param name="name">原始列名.</param>
    /// <param name="used">已占用的列名集合（命中后会写入新名称）.</param>
    /// <returns>可安全用作字典键的列名.</returns>
    private static string EnsureUniqueName(string name, HashSet<string> used)
    {
        if (used.Add(name))
        {
            return name;
        }

        var index = 2;
        var candidate = name + DuplicateSuffixSeparator + index.ToString(CultureInfo.InvariantCulture);
        while (!used.Add(candidate))
        {
            index++;
            candidate = name + DuplicateSuffixSeparator + index.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    /// <summary>
    /// 把驱动返回的值归一为可安全序列化的形态.
    /// </summary>
    /// <param name="value">驱动返回值.</param>
    /// <returns>归一后的值.</returns>
    private static object? Normalize(object? value)
    {
        return value switch
        {
            null or DBNull => null,
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value,
        };
    }
}
