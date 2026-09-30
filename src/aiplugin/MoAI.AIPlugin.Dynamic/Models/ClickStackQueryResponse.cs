using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickStack 查询插件响应（按 Mode 只填充对应集合，其余为空）.
/// </summary>
public class ClickStackQueryResponse
{
    /// <summary>
    /// 结果类型：sources / search / chart.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// sources 模式：数据源列表.
    /// </summary>
    public IReadOnlyList<ClickStackSource> Sources { get; set; } = [];

    /// <summary>
    /// search 模式：结果行（列名 → 值）.
    /// </summary>
    [Description("结果行：每行为列名到值的映射（列名与值形态来自 ClickHouse 原样）")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>
    /// search 模式：本次返回行数.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// 是否因行数上限被截断.
    /// </summary>
    [Description("true=返回行数达到上限，可能还有更多行；可用 Offset 翻页或收紧 Where/时间窗")]
    public bool Truncated { get; set; }

    /// <summary>
    /// chart 模式：时间线数据点.
    /// </summary>
    public IReadOnlyList<ClickStackChartPoint> Points { get; set; } = [];
}

/// <summary>
/// ClickStack 数据源.
/// </summary>
public class ClickStackSource
{
    /// <summary>
    /// 数据源 ID（search/chart 的 SourceId）.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 数据源名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 数据源类型：log / trace / metric / session / promql.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// ClickHouse 库名.
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// ClickHouse 表名（metric 源无 from.tableName 时回退第一个指标表名）.
    /// </summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>
    /// 默认返回列（search 的 Select 可参考）.
    /// </summary>
    public string DefaultSelect { get; set; } = string.Empty;

    /// <summary>
    /// 是否已停用.
    /// </summary>
    public bool Disabled { get; set; }
}

/// <summary>
/// ClickStack 时间线数据点.
/// </summary>
public class ClickStackChartPoint
{
    /// <summary>
    /// 时间桶（ISO 8601 UTC）.
    /// </summary>
    public string TsBucket { get; set; } = string.Empty;

    /// <summary>
    /// 该桶的聚合值（无值时为 null）.
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// 分组字段值（按请求 GroupBy 顺序）.
    /// </summary>
    public IReadOnlyList<string> Group { get; set; } = [];
}
