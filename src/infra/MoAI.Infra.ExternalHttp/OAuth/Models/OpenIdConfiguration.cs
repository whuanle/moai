
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
using System.Text.Json.Serialization;

namespace MoAI.Infra.OAuth.Models;

/// <summary>
/// OpenIdConfiguration.
/// </summary>
public class OpenIdConfiguration
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; }

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; }

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; }

    [JsonPropertyName("userinfo_endpoint")]
    public string UserinfoEndpoint { get; set; }

    [JsonPropertyName("device_authorization_endpoint")]
    public string DeviceAuthorizationEndpoint { get; set; }

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; }

    [JsonPropertyName("introspection_endpoint")]
    public string IntrospectionEndpoint { get; set; }

    [JsonPropertyName("response_types_supported")]

    public List<string> ResponseTypesSupported { get; set; }

    [JsonPropertyName("response_modes_supported")]
    public List<string> ResponseModesSupported { get; set; }

    [JsonPropertyName("grant_types_supported")]
    public List<string> GrantTypesSupported { get; set; }

    [JsonPropertyName("subject_types_supported")]
    public List<string> SubjectTypesSupported { get; set; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public List<string> IdTokenSigningAlgValuesSupported { get; set; }

    [JsonPropertyName("scopes_supported")]
    public List<string> ScopesSupported { get; set; }

    [JsonPropertyName("claims_supported")]
    public List<string> ClaimsSupported { get; set; }

    [JsonPropertyName("request_parameter_supported")]
    public bool RequestParameterSupported { get; set; }

    [JsonPropertyName("request_object_signing_alg_values_supported")]
    public List<string> RequestObjectSigningAlgValuesSupported { get; set; }

    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; set; }
}