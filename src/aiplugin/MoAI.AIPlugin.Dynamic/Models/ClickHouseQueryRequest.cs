using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickHouse 查询插件请求参数.
/// </summary>
public class ClickHouseQueryRequest
{
    /// <summary>
    /// 只读 SQL.
    /// </summary>
    [Description("要执行的只读 SQL（单条）：先用 SHOW DATABASES 摸库、SHOW TABLES FROM 库名 摸表、DESCRIBE TABLE 库.表 看列结构（或查 system.columns），再用 SELECT ... LIMIT n 查询；仅允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN 等读语句，末尾 FORMAT 子句会被剥离（出参格式固定 JSON）")]
    public string Sql { get; set; } = string.Empty;
}
