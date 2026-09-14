namespace MoAI.Settings.Models;

/// <summary>
/// 知识图谱图数据库配置（供业务模块读取，是否开启由超级管理员在系统设置页控制）.
/// </summary>
public class KnowledgeGraphStoreSettings
{
    /// <summary>
    /// 方言：Memgraph（社区版，默认）.
    /// </summary>
    public const string DialectMemgraph = "memgraph";

    /// <summary>
    /// 方言：Neo4j.
    /// </summary>
    public const string DialectNeo4j = "neo4j";

    /// <summary>
    /// 是否开启知识图谱.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 图数据库 Bolt 连接地址.
    /// </summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>
    /// 图数据库用户名.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 图数据库密码.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// 方言（memgraph / neo4j），影响探活、内省与索引语句.
    /// </summary>
    public string Dialect { get; init; } = DialectMemgraph;

    /// <summary>
    /// 是否为 Neo4j 方言.
    /// </summary>
    public bool IsNeo4j => string.Equals(Dialect, DialectNeo4j, StringComparison.OrdinalIgnoreCase);
}
