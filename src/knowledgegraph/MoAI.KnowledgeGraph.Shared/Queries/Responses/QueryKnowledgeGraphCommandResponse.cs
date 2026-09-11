using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱详情响应.
/// </summary>
public class QueryKnowledgeGraphCommandResponse
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 模板 key.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <summary>
    /// 我的角色.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 能力是否开启.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 来源：managed / connected.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入数据库名（仅 connected）.
    /// </summary>
    public string? Database { get; init; }

    /// <summary>
    /// 是否只读（connected=true）.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
