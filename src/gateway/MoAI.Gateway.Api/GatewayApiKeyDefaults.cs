namespace MoAI.Gateway;

/// <summary>
/// 网关 API Key 认证方案常量：接入统一使用应用接入 key（moai-ac-，access_app）.
/// </summary>
public static class GatewayApiKeyDefaults
{
    /// <summary>
    /// 认证方案名.
    /// </summary>
    public const string AuthenticationScheme = "GatewayApiKey";

    /// <summary>
    /// 应用接入 key 前缀.
    /// </summary>
    public const string AccessAppKeyPrefix = "moai-ac-";

    /// <summary>
    /// 功能范围 claim，值为逗号分隔的 scope 代码（model/wiki_read/...）.
    /// </summary>
    public const string ClaimScopes = "scopes";
}
