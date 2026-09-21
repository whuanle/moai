using System.Collections.Generic;
using System.ComponentModel;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 知识图谱只读查询插件响应结果：Schema=true 时填摘要字段，否则填表格字段.
/// </summary>
public class KgCypherQueryResponse
{
    /// <summary>结果列名.</summary>
    [Description("查询模式：结果列名，按查询返回顺序排列")]
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>结果行.</summary>
    [Description("查询模式：结果行，每行为列名到值的映射；节点返回 {_id, _labels, 属性...}，关系返回 {_id, _type, 属性...}，超长字符串截断为 2000 字符")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>本次返回行数.</summary>
    [Description("查询模式：本次实际返回行数")]
    public int RowCount { get; set; }

    /// <summary>是否被截断.</summary>
    [Description("查询模式：是否因超过 maxRows 被截断；true 表示图中还有更多结果未返回")]
    public bool Truncated { get; set; }

    /// <summary>图谱类型.</summary>
    [Description("自描述模式：图谱类型 managed（托管）/ connected（外部接入）")]
    public string? GraphType { get; set; }

    /// <summary>图数据库方言.</summary>
    [Description("自描述模式：图数据库方言 memgraph/neo4j")]
    public string? Dialect { get; set; }

    /// <summary>实体类型清单.</summary>
    [Description("自描述模式：实体类型清单（名称、描述、属性）")]
    public IReadOnlyList<KgCypherSchemaItem> EntityTypes { get; set; } = [];

    /// <summary>关系类型清单.</summary>
    [Description("自描述模式：关系类型清单（名称、起止类型、描述）")]
    public IReadOnlyList<KgCypherRelationTypeItem> RelationTypes { get; set; } = [];

    /// <summary>采样节点.</summary>
    [Description("自描述模式：各类型节点名采样")]
    public IReadOnlyList<KgCypherSampleGroup> SampleNodes { get; set; } = [];

    /// <summary>属性键清单.</summary>
    [Description("自描述模式：接入图属性键清单（托管图为空）")]
    public IReadOnlyList<string> PropertyKeys { get; set; } = [];

    /// <summary>用法说明.</summary>
    [Description("自描述模式：该图谱的查询用法说明（含 $kgId 指引）")]
    public string? Usage { get; set; }
}
