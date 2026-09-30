using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SQL Server 只读查询插件请求参数.
/// </summary>
public class SqlServerQueryRequest
{
    /// <summary>
    /// 只读 SQL.
    /// </summary>
    [Description("只读 SQL（单条）：仅允许 SELECT/WITH/TABLE/VALUES/SHOW/EXPLAIN/DESCRIBE 开头的查询语句，写操作/DDL/多条语句会被拒绝")]
    public string Sql { get; set; } = string.Empty;
}
