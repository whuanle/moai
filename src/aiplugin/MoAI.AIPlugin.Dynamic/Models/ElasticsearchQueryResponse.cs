using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Elasticsearch 查询插件响应结果.
/// </summary>
public class ElasticsearchQueryResponse
{
    /// <summary>
    /// 实际使用的操作模式.
    /// </summary>
    [Description("实际使用的操作模式：search/count/mappings/indices")]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// 服务端处理耗时（毫秒）.
    /// </summary>
    [Description("search/count 模式：服务端处理耗时（毫秒）")]
    public long TookMs { get; set; }

    /// <summary>
    /// 总命中数.
    /// </summary>
    [Description("search/count 模式：总命中数（count 模式即计数结果）")]
    public long TotalHits { get; set; }

    /// <summary>
    /// 最高得分.
    /// </summary>
    [Description("search 模式：最高得分文本（无得分时为空）")]
    public string? MaxScore { get; set; }

    /// <summary>
    /// 命中列表.
    /// </summary>
    [Description("search 模式：命中列表；仅返回索引/文档 id/得分/_source 文本")]
    public IReadOnlyList<ElasticsearchHit> Hits { get; set; } = [];

    /// <summary>
    /// 聚合结果（原样 JSON 文本）.
    /// </summary>
    [Description("search 模式：DSL 中含 aggs 时的聚合结果，原样 JSON 文本（超长被截断）")]
    public string? AggregationsJson { get; set; }

    /// <summary>
    /// count 模式的计数结果.
    /// </summary>
    [Description("count 模式：命中文档数（同 TotalHits）")]
    public long? Count { get; set; }

    /// <summary>
    /// mappings 模式：字段映射原文 JSON.
    /// </summary>
    [Description("mappings 模式：索引字段映射原文 JSON（超长被截断）")]
    public string? MappingJson { get; set; }

    /// <summary>
    /// indices 模式：索引清单.
    /// </summary>
    [Description("indices 模式：索引清单（_cat/indices：健康/状态/名称/主副分片/文档数/存储）")]
    public IReadOnlyList<ElasticsearchIndexRow> Indices { get; set; } = [];

    /// <summary>
    /// 是否被上限截断.
    /// </summary>
    [Description("是否因 MaxHits/MaxResponseChars 被截断；true 表示服务端还有更多数据未返回")]
    public bool Truncated { get; set; }
}
