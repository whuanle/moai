using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 团队资源 key 的资源类型：teamres_key 表按（团队, 类型, 资源）一行一把 key.
/// </summary>
public enum TeamResourceType
{
    /// <summary>
    /// 团队应用（ACP/A2A 共用一把，团队级，resource_id=0）.
    /// </summary>
    [JsonPropertyName("app")]
    App = 1,

    /// <summary>
    /// 知识库（resource_id=wikiId，覆盖该库外部 REST 接口与 MCP 端点）.
    /// </summary>
    [JsonPropertyName("wiki")]
    Wiki = 2,

    /// <summary>
    /// 知识图谱（resource_id=kgId，覆盖该图谱外部 REST 接口与 MCP 端点）.
    /// </summary>
    [JsonPropertyName("knowledgeGraph")]
    KnowledgeGraph = 3,
}
