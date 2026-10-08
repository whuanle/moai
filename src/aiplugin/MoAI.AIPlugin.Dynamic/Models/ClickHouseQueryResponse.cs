using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickHouse 查询插件响应结果.
/// </summary>
public class ClickHouseQueryResponse
{
    /// <summary>
    /// 结果集列名.
    /// </summary>
    [Description("结果集列名，按查询返回顺序排列（来自 FORMAT JSON 的 meta，空结果集也有列信息）")]
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// 各列的 ClickHouse 类型.
    /// </summary>
    [Description("各列的 ClickHouse 类型（与 Columns 一一对应）")]
    public IReadOnlyList<string> ColumnTypes { get; set; } = [];

    /// <summary>
    /// 结果行：每行为「列名 → 值」的映射.
    /// </summary>
    [Description("结果行：每行为列名到值的映射；Map/嵌套类型逐层归一为对象，NaN/+Inf 为文本")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>
    /// 本次实际返回的行数.
    /// </summary>
    [Description("本次实际返回的行数")]
    public int RowCount { get; set; }

    /// <summary>
    /// 是否因行数上限被截断.
    /// </summary>
    [Description("是否因 MaxRows 被截断；true 表示库里还有更多行未返回，可收紧过滤或加 LIMIT 后续查")]
    public bool Truncated { get; set; }
}
