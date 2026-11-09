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
    public long KgId { get; init; }

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
    /// 向量化模型 id（元素为 ai_model.id 的 uuid）；未配置向量化时为 null.
    /// </summary>
    public Guid? EmbeddingModelId { get; init; }

    /// <summary>
    /// 默认抽取模型 id（AI 导入缺省对话模型）；为空表示未配置.
    /// </summary>
    public Guid? ExtractModelId { get; init; }

    /// <summary>
    /// 画布样式规则（键=实体类型 id 字符串，值=属性名或空串表示不显示第二行）；null=未配置（画布按默认显示类型名）.
    /// </summary>
    public Dictionary<string, string>? CanvasStyle { get; init; }

    /// <summary>
    /// 向量维度；未配置向量化时为 0.
    /// </summary>
    public int EmbeddingDimensions { get; init; }

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
    /// 头像 ObjectKey（空串表示未设置，前端经 /static/{key} 解析）.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
