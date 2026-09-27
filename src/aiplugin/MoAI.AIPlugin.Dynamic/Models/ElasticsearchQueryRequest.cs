using System.ComponentModel;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Elasticsearch 查询插件请求参数.
/// </summary>
public class ElasticsearchQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：search=DSL 检索（可含聚合，默认）/ count=DSL 计数 / mappings=字段映射 / indices=索引清单（_cat/indices）；未知值会被拒绝")]
    public string Mode { get; set; } = "search";

    /// <summary>
    /// 索引名（支持通配符与逗号分隔；空值按 _all 全库检索）.
    /// </summary>
    [Description("索引名，支持通配符与逗号分隔多索引，如 logs-*、logs-2026.09.26；留空表示全部索引（_all）")]
    public string Index { get; set; } = "_all";

    /// <summary>
    /// 查询 DSL（JSON 对象）.
    /// </summary>
    [Description("search/count 模式的查询 DSL（JSON 对象）：支持 query/aggs/sort/from/size/_source/track_total_hits/highlight 等键；omitted 时按 match_all 处理；为只读检索端点，不会写入数据")]
    public JsonElement Dsl { get; set; }
}
