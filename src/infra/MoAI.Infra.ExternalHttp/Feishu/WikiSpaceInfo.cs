#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using System.Text.Json.Serialization;
using MoAI.Infra.Feishu.Models;
using Refit;

namespace MoAI.Infra.Feishu;

/// <summary>
/// 知识空间详细信息.
/// </summary>
public class WikiSpaceInfo
{
    /// <summary>
    /// 知识空间名称.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// 知识空间描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; }

    /// <summary>
    /// 知识空间 ID.
    /// </summary>
    [JsonPropertyName("space_id")]
    public string SpaceId { get; init; }

    /// <summary>
    /// 知识空间类型.
    /// </summary>
    [JsonPropertyName("space_type")]
    public string SpaceType { get; init; }

    /// <summary>
    /// 知识空间可见性.
    /// </summary>
    [JsonPropertyName("visibility")]
    public string Visibility { get; init; }

    /// <summary>
    /// 知识空间的分享状态.
    /// </summary>
    [JsonPropertyName("open_sharing")]
    public string OpenSharing { get; init; }
}