using System.Collections.Generic;

namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 图 Cypher 只读查询结果（表格化）.
/// </summary>
/// <param name="Columns">结果列名（取自第一条记录的返回键）.</param>
/// <param name="Rows">结果行，每行为「列名 → 值」映射.</param>
/// <param name="RowCount">本次实际返回行数.</param>
/// <param name="Truncated">是否因行数上限被截断.</param>
public sealed record KgCypherQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int RowCount,
    bool Truncated);

/// <summary>
/// 实体类型摘要项.
/// </summary>
/// <param name="Name">类型名.</param>
/// <param name="Description">描述.</param>
/// <param name="Properties">属性名清单.</param>
public sealed record KgCypherSchemaItem(string Name, string Description, IReadOnlyList<string> Properties);

/// <summary>
/// 关系类型摘要项.
/// </summary>
/// <param name="Name">类型名.</param>
/// <param name="FromTypeName">起始实体类型名（未约束为 null）.</param>
/// <param name="ToTypeName">目标实体类型名（未约束为 null）.</param>
/// <param name="Description">描述.</param>
public sealed record KgCypherRelationTypeItem(string Name, string? FromTypeName, string? ToTypeName, string Description);

/// <summary>
/// 节点名采样分组.
/// </summary>
/// <param name="TypeName">实体类型名（接入图为 label）.</param>
/// <param name="Names">采样节点名.</param>
public sealed record KgCypherSampleGroup(string TypeName, IReadOnlyList<string> Names);

/// <summary>
/// 图谱自描述摘要（供对话模型写 Cypher 前了解图结构）.
/// </summary>
/// <param name="GraphType">managed / connected.</param>
/// <param name="Dialect">图数据库方言（memgraph/neo4j）.</param>
/// <param name="EntityTypes">实体类型清单（接入图为空）.</param>
/// <param name="RelationTypes">关系类型清单（接入图为空）.</param>
/// <param name="SampleNodes">各类型节点名采样.</param>
/// <param name="PropertyKeys">接入图属性键清单（托管图为空）.</param>
/// <param name="Usage">用法说明（含 $kgId 指引）.</param>
public sealed record KgCypherSchemaDigest(
    string GraphType,
    string Dialect,
    IReadOnlyList<KgCypherSchemaItem> EntityTypes,
    IReadOnlyList<KgCypherRelationTypeItem> RelationTypes,
    IReadOnlyList<KgCypherSampleGroup> SampleNodes,
    IReadOnlyList<string> PropertyKeys,
    string Usage);
