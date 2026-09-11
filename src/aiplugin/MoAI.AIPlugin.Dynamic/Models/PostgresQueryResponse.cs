using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PostgreSQL 只读查询插件响应结果.
/// </summary>
public class PostgresQueryResponse
{
    /// <summary>
    /// 结果集列名，按查询返回顺序排列.
    /// </summary>
    [Description("结果集列名，按查询返回顺序排列；出现同名列时自动追加 _2、_3 后缀")]
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// 结果行：每行为「列名 → 值」的映射.
    /// </summary>
    [Description("结果行：每行为列名到值的映射；二进制列（bytea）以 Base64 文本返回")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>
    /// 本次实际返回的行数.
    /// </summary>
    [Description("本次实际返回的行数")]
    public int RowCount { get; set; }

    /// <summary>
    /// 是否因超过 MaxRows 被截断.
    /// </summary>
    [Description("是否因超过 MaxRows 被截断；true 表示数据库中还有更多行未返回")]
    public bool Truncated { get; set; }
}
