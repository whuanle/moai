namespace MoAI.Settings.Models;

/// <summary>
/// Neo4j 知识图谱配置（供业务模块读取，是否开启由超级管理员在系统设置页控制）.
/// </summary>
public class Neo4jKnowledgeGraphSettings
{
    /// <summary>
    /// 是否开启知识图谱.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Neo4j 连接地址.
    /// </summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>
    /// Neo4j 用户名.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Neo4j 密码.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}
