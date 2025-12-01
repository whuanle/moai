using System.Text.Json.Serialization;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 知识库查询类型.
/// </summary>
public enum WikiQueryType
{
    /// <summary>
    /// 无限制.
    /// </summary>
    [JsonPropertyName("none")]
    None,

    /// <summary>
    /// 自己创建的知识库.
    /// </summary>
    [JsonPropertyName("own")]
    Own,

    /// <summary>
    /// 已加入的知识库.
    /// </summary>
    [JsonPropertyName("own")]
    User,

    /// <summary>
    /// 公开的.
    /// </summary>
    [JsonPropertyName("public")]
    Public
}