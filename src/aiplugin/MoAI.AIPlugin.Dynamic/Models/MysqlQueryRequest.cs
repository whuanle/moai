using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// MySQL 只读查询插件请求参数.
/// </summary>
public class MysqlQueryRequest
{
    /// <summary>
    /// 要执行的只读 SQL（单条查询语句）.
    /// </summary>
    [Description("要执行的只读 SQL：必须是单条查询语句（SELECT/WITH/TABLE/VALUES/SHOW/EXPLAIN/DESCRIBE），不允许出现写操作、DDL 或多条语句")]
    public string Sql { get; set; } = string.Empty;
}
