#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 文档展示设置。
/// </summary>
public class DocumentDisplaySetting
{
    /// <summary>
    /// 文档信息中是否展示文档作者。
    /// </summary>
    [JsonPropertyName("show_authors")]
    public bool ShowAuthors { get; init; }

    /// <summary>
    /// 文档信息中是否展示文档创建时间。
    /// </summary>
    [JsonPropertyName("show_create_time")]
    public bool ShowCreateTime { get; init; }

    /// <summary>
    /// 文档信息中是否展示文档访问次数。
    /// </summary>
    [JsonPropertyName("show_pv")]
    public bool ShowPv { get; init; }

    /// <summary>
    /// 文档信息中是否展示文档访问人数。
    /// </summary>
    [JsonPropertyName("show_uv")]
    public bool ShowUv { get; init; }

    /// <summary>
    /// 文档信息中是否展示点赞总数。
    /// </summary>
    [JsonPropertyName("show_like_count")]
    public bool ShowLikeCount { get; init; }

    /// <summary>
    /// 文档信息中是否展示评论总数。
    /// </summary>
    [JsonPropertyName("show_comment_count")]
    public bool ShowCommentCount { get; init; }

    /// <summary>
    /// 文档信息中是否展示关联事项。
    /// </summary>
    [JsonPropertyName("show_related_matters")]
    public bool ShowRelatedMatters { get; init; }
}
