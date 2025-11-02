
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuUserInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("en_name")]
    public string EnName { get; set; }

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }

    [JsonPropertyName("avatar_thumb")]
    public string AvatarThumb { get; set; }

    [JsonPropertyName("avatar_middle")]
    public string AvatarMiddle { get; set; }

    [JsonPropertyName("avatar_big")]
    public string AvatarBig { get; set; }

    [JsonPropertyName("open_id")]
    public string OpenId { get; set; }

    [JsonPropertyName("union_id")]
    public string UnionId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("enterprise_email")]
    public string EnterpriseEmail { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("mobile")]
    public string Mobile { get; set; }

    [JsonPropertyName("tenant_key")]
    public string TenantKey { get; set; }

    [JsonPropertyName("employee_no")]
    public string EmployeeNo { get; set; }
}