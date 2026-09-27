namespace MoAI.App.Services;

/// <summary>
/// 外部接入（/api/external）认证常量：外部 token 与内部 JWT 同钥签发但 audience 不同，内外接口天然隔离.
/// </summary>
public static class ExternalAuthDefaults
{
    /// <summary>
    /// 外部认证方案名.
    /// </summary>
    public const string AuthenticationScheme = "ExternalJwt";

    /// <summary>
    /// 外部 token 的 audience 后缀，实际 audience = SystemOptions.Server + 此后缀.
    /// </summary>
    public const string AudienceSuffix = "|external";

    /// <summary>
    /// 应用接入 key 前缀.
    /// </summary>
    public const string AccessAppKeyPrefix = "moai-ac-";

    /// <summary>
    /// token 类型 claim.
    /// </summary>
    public const string ClaimTokenType = "token_type";

    /// <summary>
    /// 归属团队 claim.
    /// </summary>
    public const string ClaimTeamId = "teamid";

    /// <summary>
    /// 来源应用接入 claim.
    /// </summary>
    public const string ClaimAccessAppId = "accessappid";

    /// <summary>
    /// 授权单应用 claim（用户 token）.
    /// </summary>
    public const string ClaimAppId = "appid";

    /// <summary>
    /// 外部身份标识 claim.
    /// </summary>
    public const string ClaimExternalUserId = "externaluserid";

    /// <summary>
    /// 功能范围 claim，值为逗号分隔的 scope 代码（wiki_read/wiki_write/...）；缺省视为旧 token，按全量知识库范围处理.
    /// </summary>
    public const string ClaimScope = "scope";

    /// <summary>
    /// access token 类型值.
    /// </summary>
    public const string TokenTypeAccess = "access_token";

    /// <summary>
    /// refresh token 类型值.
    /// </summary>
    public const string TokenTypeRefresh = "refresh_token";

    /// <summary>
    /// HttpContext.Items 中存放 <see cref="Models.ExternalTokenContext"/> 的 key.
    /// </summary>
    public const string TokenContextItemKey = "MoAI.ExternalTokenContext";

    /// <summary>
    /// key 直连会话身份的固定外部身份标识：按（来源应用接入 id, 此标识）唯一，
    /// 应用接入 key 直连发起会话/对话时复用为 external_user 记录，会话归属延续 CreateUserId 语义.
    /// </summary>
    public const string KeyDirectExternalUserId = "__key_direct__";

    /// <summary>
    /// 构造外部 token 的 audience.
    /// </summary>
    /// <param name="server">服务器地址（SystemOptions.Server）.</param>
    /// <returns>返回外部 audience.</returns>
    public static string BuildAudience(string server) => $"{server}{AudienceSuffix}";
}
