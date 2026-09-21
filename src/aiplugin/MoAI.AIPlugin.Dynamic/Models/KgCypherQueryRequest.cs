using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 知识图谱只读查询插件请求参数.
/// </summary>
public class KgCypherQueryRequest
{
    /// <summary>
    /// 要执行的只读 Cypher 查询.
    /// </summary>
    [Description("要执行的只读 Cypher 查询语句。托管图谱必须包含 {kgId: $kgId} 过滤（$kgId 由系统自动注入，无需赋值），例如 MATCH (n:KgNode {kgId: $kgId}) WHERE n.name CONTAINS $kw RETURN n.name, n.description LIMIT 20")]
    public string Cypher { get; set; } = string.Empty;

    /// <summary>
    /// Cypher 查询参数.
    /// </summary>
    [Description("Cypher 查询参数对象：键为 $ 参数名（不含 $ 前缀），值只能是字符串/数字/布尔；kgId 为系统保留键，传入会被覆盖")]
#pragma warning disable CA2227 // STJ 反序列化需 set 访问器；Dictionary 可写集合属性触发 CA2227（同工程 IReadOnlyList 属性不受影响）
    public Dictionary<string, object?>? Params { get; set; }
#pragma warning restore CA2227

    /// <summary>
    /// 是否返回图谱自描述.
    /// </summary>
    [Description("置为 true 时返回图谱自描述摘要（实体类型、关系类型、示例节点与用法说明），此时忽略 Cypher；首次查询前建议先取摘要")]
    public bool Schema { get; set; }
}
