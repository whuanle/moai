#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// Wiki 节点信息。
/// </summary>
public class WikiNode
{
    /// <summary>
    /// 知识空间 id。
    /// </summary>
    [JsonPropertyName("space_id")]
    public string SpaceId { get; init; }

    /// <summary>
    /// 节点 token。
    /// </summary>
    [JsonPropertyName("node_token")]
    public string NodeToken { get; init; }

    /// <summary>
    /// 对应文档类型的 token。
    /// </summary>
    [JsonPropertyName("obj_token")]
    public string ObjToken { get; init; }

    /// <summary>
    /// 文档类型。
    /// </summary>
    /// <remarks>
    /// 可选值有：doc, sheet, mindnote, bitable, file。
    /// </remarks>
    [JsonPropertyName("obj_type")]
    public string ObjType { get; init; }

    /// <summary>
    /// 父节点 token。若当前节点为一级节点，父节点 token 为空。
    /// </summary>
    [JsonPropertyName("parent_node_token")]
    public string ParentNodeToken { get; init; }

    /// <summary>
    /// 节点类型。
    /// </summary>
    /// <remarks>
    /// 可选值有：origin, shortcut。
    /// </remarks>
    [JsonPropertyName("node_type")]
    public string NodeType { get; init; }

    /// <summary>
    /// 快捷方式对应的实体 node_token。
    /// </summary>
    [JsonPropertyName("origin_node_token")]
    public string OriginNodeToken { get; init; }

    /// <summary>
    /// 快捷方式对应的实体所在的 space id。
    /// </summary>
    [JsonPropertyName("origin_space_id")]
    public string OriginSpaceId { get; init; }

    /// <summary>
    /// 是否有子节点。
    /// </summary>
    [JsonPropertyName("has_child")]
    public bool HasChild { get; init; }

    /// <summary>
    /// 文档标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; }

    /// <summary>
    /// 文档创建时间。
    /// </summary>
    [JsonPropertyName("obj_create_time")]
    public string ObjCreateTime { get; init; }

    /// <summary>
    /// 文档最近编辑时间。
    /// </summary>
    [JsonPropertyName("obj_edit_time")]
    public string ObjEditTime { get; init; }

    /// <summary>
    /// 节点创建时间。
    /// </summary>
    [JsonPropertyName("node_create_time")]
    public string NodeCreateTime { get; init; }

    /// <summary>
    /// 节点创建者。
    /// </summary>
    [JsonPropertyName("creator")]
    public string Creator { get; init; }

    /// <summary>
    /// 节点所有者。
    /// </summary>
    [JsonPropertyName("owner")]
    public string Owner { get; init; }
}