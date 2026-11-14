
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
using System.Text.Json.Serialization;

namespace MoAI.Infra.OAuth.Models;

public class OpenIdUserProfile
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; }

    /// <summary>
    /// 租户级唯一标识（飞书 union_id 等，跨应用一致），仅部分供应商返回，可为空.
    /// </summary>
    [JsonPropertyName("union_id")]
    public string? UnionId { get; set; }

    [JsonPropertyName("iss")]
    public string Issuer { get; set; }

    [JsonPropertyName("aud")]
    public string Audience { get; set; }

    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("picture")]
    public string Picture { get; set; }
}
