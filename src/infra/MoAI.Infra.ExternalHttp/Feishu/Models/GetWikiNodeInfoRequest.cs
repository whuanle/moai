#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 获取知识空间节点信息的请求。
/// </summary>
public class GetWikiNodeInfoRequest
{
    /// <summary>
    /// 节点 token 或对应云文档 token。
    /// </summary>
    /// <example>wikcnKQ1k3p******8Vabcef</example>
    [AliasAs("token")]
    [JsonPropertyName("token")]
    public string Token { get; init; } = default!;

    /// <summary>
    /// 文档类型，不传默认按 wiki 查询。
    /// </summary>
    /// <remarks>支持 doc、docx、sheet、mindnote、bitable、file、slides、wiki 等。</remarks>
    /// <example>docx</example>
    [AliasAs("obj_type")]
    [JsonPropertyName("obj_type")]
    public string? ObjType { get; init; }
}