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
    [Description("结果集列名，按查询返回顺序排列")]
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// 各列的 ClickHouse 类型（便捷查询模式）.
    /// </summary>
    [Description("各列的 ClickHouse 类型（traces/logs/metrics 模式从 system.columns 自描述而来；sql 模式为空）")]
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
    [Description("是否因 MaxRows/Limit 被截断；true 表示库里还有更多行未返回")]
    public bool Truncated { get; set; }

    /// <summary>
    /// 防御性说明（为空时无）.
    /// </summary>
    [Description("防御性说明：老版本 schema 缺列、Body 为 Map 忽略 SearchText 等情况下给出可读说明")]
    public string Notice { get; set; } = string.Empty;
}
