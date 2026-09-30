using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SQL Server 只读查询插件响应（列名 + 行数据）.
/// </summary>
public class SqlServerQueryResponse
{
    /// <summary>
    /// 列名（按查询返回顺序，重名列自动加 _2/_3 后缀）.
    /// </summary>
    public IReadOnlyList<string> Columns { get; set; } = new List<string>();

    /// <summary>
    /// 行数据（每行「列名 → 值」字典）.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = new List<IReadOnlyDictionary<string, object?>>();

    /// <summary>
    /// 返回行数.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// 是否因超过 MaxRows 被截断.
    /// </summary>
    public bool Truncated { get; set; }
}
