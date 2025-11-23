#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 文档基本信息。
/// </summary>
public class DocumentInfo
{
    /// <summary>
    /// 文档的唯一标识。
    /// </summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; }

    /// <summary>
    /// 文档版本 ID。
    /// </summary>
    [JsonPropertyName("revision_id")]
    public int RevisionId { get; init; }

    /// <summary>
    /// 文档标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; }

    /// <summary>
    /// 文档展示设置。
    /// </summary>
    [JsonPropertyName("display_setting")]
    public DocumentDisplaySetting DisplaySetting { get; init; }

    /// <summary>
    /// 文档封面。
    /// </summary>
    [JsonPropertyName("cover")]
    public DocumentCover Cover { get; init; }
}
