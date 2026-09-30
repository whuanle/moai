using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickStack 查询插件请求参数.
/// </summary>
public class ClickStackQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：sources=列数据源（拿 sourceId，起步先调这个）/ search=原始日志与 trace 检索（默认）/ chart=时间线聚合；未知值会被拒绝")]
    public string Mode { get; set; } = "search";

    /// <summary>
    /// 数据源 ID.
    /// </summary>
    [Description("数据源 ID（sources 模式获取）：search 与 chart 必填")]
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// 行过滤表达式.
    /// </summary>
    [Description("行过滤表达式：search/chart 生效。Lucene 语法（如 SeverityText:ERROR AND ServiceName:moai）；WhereLanguage=sql 时为 SQL WHERE 片段；≤8192 字符，可空=不过滤")]
    public string Where { get; set; } = string.Empty;

    /// <summary>
    /// Where 的语言.
    /// </summary>
    [Description("Where 的语言：lucene（默认）或 sql")]
    public string WhereLanguage { get; set; } = "lucene";

    /// <summary>
    /// search 专属：返回列.
    /// </summary>
    [Description("search 专属：select 列表达式，逗号分隔（如 Timestamp,ServiceName,Body），可空=数据源默认列；服务端拒绝分号与子查询，≤4096")]
    public string Select { get; set; } = string.Empty;

    /// <summary>
    /// search 专属：排序.
    /// </summary>
    [Description("search 专属：排序表达式（如 Timestamp DESC），≤1024，可空=服务端默认（时间倒序）")]
    public string OrderBy { get; set; } = string.Empty;

    /// <summary>
    /// 时间窗起点（ISO 8601）.
    /// </summary>
    [Description("时间窗起点（ISO 8601，如 2026-09-29T00:00:00Z，也接受 2026-09-29 08:00:00）：search 缺省=End-15 分钟；chart 缺省=End-1 小时")]
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// 时间窗终点（ISO 8601）.
    /// </summary>
    [Description("时间窗终点（ISO 8601）：缺省=当前时间")]
    public string EndTime { get; set; } = string.Empty;

    /// <summary>
    /// search 专属：最大行数.
    /// </summary>
    [Description("search 专属：本次最大行数，取值 1-2000（默认取配置 MaxRows）")]
    public int MaxResults { get; set; }

    /// <summary>
    /// search 专属：分页偏移.
    /// </summary>
    [Description("search 专属：分页偏移，取值 0-10000（默认 0）")]
    public int Offset { get; set; }

    /// <summary>
    /// chart 专属：聚合粒度.
    /// </summary>
    [Description("chart 专属：聚合粒度，30s/1m/5m/10m/15m/30m/1h/2h/6h/12h/1d/2d/7d/30d/auto（默认 1h）")]
    public string Granularity { get; set; } = string.Empty;

    /// <summary>
    /// chart 专属：聚合函数.
    /// </summary>
    [Description("chart 专属：聚合函数 avg/count/count_distinct/last_value/max/min/quantile/sum（默认 count）")]
    public string AggFn { get; set; } = string.Empty;

    /// <summary>
    /// chart 专属：聚合字段.
    /// </summary>
    [Description("chart 专属：聚合字段（count 可空；其余必填，如 Body、duration 毫秒列名）")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// chart 专属：分组字段.
    /// </summary>
    [Description("chart 专属：分组字段，逗号分隔（如 ServiceName,SeverityText），可空=不分组")]
    public string GroupBy { get; set; } = string.Empty;
}
