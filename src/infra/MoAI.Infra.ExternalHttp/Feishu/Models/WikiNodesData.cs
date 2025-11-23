#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 知识空间子节点列表数据。
/// </summary>
public class WikiNodesData
{
    /// <summary>
    /// 数据列表。
    /// </summary>
    [JsonPropertyName("items")]
    public List<WikiNode> Items { get; init; }

    /// <summary>
    /// 分页标记，当 has_more 为 true 时，会同时返回新的 page_token，否则不返回 page_token。
    /// </summary>
    [JsonPropertyName("page_token")]
    public string PageToken { get; init; }

    /// <summary>
    /// 是否还有更多项。
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}
