namespace MoAI.Subsystem.Services;

/// <summary>
/// 系统级接入机器面（/api/subsys/**）认证常量：接入 token 与内部 JWT 同钥签发但 audience 不同，天然隔离.
/// </summary>
public static class SubsystemAuthDefaults
{
    /// <summary>
    /// 接入 token 的 audience 后缀，实际 audience = SystemOptions.Server + 此后缀.
    /// </summary>
    public const string AudienceSuffix = "|subsystem";

    /// <summary>
    /// 应用接入标识 claim（appid 原文）.
    /// </summary>
    public const string ClaimAppKey = "appkey";

    /// <summary>
    /// 接入 id claim（用户委托 token 的绑定主体）.
    /// </summary>
    public const string ClaimSubsystemId = "subsystemid";

    /// <summary>
    /// token 类型 claim.
    /// </summary>
    public const string ClaimTokenType = "token_type";

    /// <summary>
    /// 机器面 access token 类型值（header typ 与 claim 同值，防混用）.
    /// </summary>
    public const string TokenTypeAccess = "access_token";

    /// <summary>
    /// 机器面 refresh token 类型值（机器 token 轮换用）.
    /// </summary>
    public const string TokenTypeMachineRefresh = "machine_refresh";

    /// <summary>
    /// 用户委托 access token 类型值.
    /// </summary>
    public const string TokenTypeUserAccess = "subsystem_user_access";

    /// <summary>
    /// 用户委托 refresh token 类型值.
    /// </summary>
    public const string TokenTypeUserRefresh = "subsystem_user_refresh";

    /// <summary>
    /// 委托码 Redis 键前缀（实际键 = 前缀 + 委托码）.
    /// </summary>
    public const string SsoCodeKeyPrefix = "subsys:sso:code:";

    /// <summary>
    /// 委托码有效期，签发后需在此时间内完成兑换.
    /// </summary>
    public static readonly TimeSpan SsoCodeLifetime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 系统级应用接入 appid 前缀（应用接入行自动生成；子系统行自填 appid 禁用该前缀）.
    /// </summary>
    public const string KeyPrefixSystemApp = "moai-sa-";

    /// <summary>
    /// HttpContext.Items 中存放 <see cref="Models.SubsysCallerContext"/> 的 key.
    /// </summary>
    public const string CallerContextItemKey = "MoAI.SubsysCallerContext";

    /// <summary>
    /// 构造接入 token 的 audience.
    /// </summary>
    /// <param name="server">服务器地址（SystemOptions.Server）.</param>
    /// <returns>返回接入 audience.</returns>
    public static string BuildAudience(string server) => $"{server}{AudienceSuffix}";

    /// <summary>
    /// 存量随机格式 appid 总长（moai-sa- + 32 位随机字符）；新格式为结构化 Base64Url（见 ISubsystemAppIdCodec）.
    /// </summary>
    public const int LegacySystemAppKeyLength = 40;

    /// <summary>
    /// key 展示前缀长度（列表 KeyPrefix 截断用）.
    /// </summary>
    public const int KeyPrefixLength = 12;

    /// <summary>
    /// 存量随机格式 appid 本地格式预检（moai-sa- 前缀 + 32 位字母/数字）：
    /// 中间件直连轨在查库前快速否决伪造/打错的 appid，扫描请求不打数据库.
    /// </summary>
    /// <param name="key">待检 appid.</param>
    /// <returns>格式合法返回 true.</returns>
    public static bool IsValidLegacySystemAppKey(string key)
    {
        if (key.Length != KeyPrefixSystemApp.Length + 32)
        {
            return false;
        }

        for (var i = KeyPrefixSystemApp.Length; i < key.Length; i++)
        {
            var c = key[i];
            var valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
