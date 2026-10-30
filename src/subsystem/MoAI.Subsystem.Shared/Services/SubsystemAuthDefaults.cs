namespace MoAI.Subsystem.Services;

/// <summary>
/// 子系统机器面（/api/subsystem/token|heartbeat|info）认证常量：子系统 token 与内部 JWT 同钥签发但 audience 不同，天然隔离.
/// </summary>
public static class SubsystemAuthDefaults
{
    /// <summary>
    /// 子系统 token 的 audience 后缀，实际 audience = SystemOptions.Server + 此后缀.
    /// </summary>
    public const string AudienceSuffix = "|subsystem";

    /// <summary>
    /// 子系统标识 claim（appid 原文）.
    /// </summary>
    public const string ClaimSubsystemKey = "subsystemkey";

    /// <summary>
    /// 子系统 id claim（用户委托 token 的绑定主体）.
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
    /// 机器面 refresh token 类型值（子系统机器 token 轮换用）.
    /// </summary>
    public const string TokenTypeMachineRefresh = "machine_refresh";

    /// <summary>
    /// 系统应用接入 access token 类型值（/api/subsys/system/* 协作面）.
    /// </summary>
    public const string TokenTypeSystemAppAccess = "system_app_access";

    /// <summary>
    /// 系统应用接入 refresh token 类型值.
    /// </summary>
    public const string TokenTypeSystemAppRefresh = "system_app_refresh";

    /// <summary>
    /// 系统应用接入标识 claim（appid 原文）.
    /// </summary>
    public const string ClaimSystemAppKey = "systemappkey";

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
    /// HttpContext.Items 中存放 <see cref="Models.SubsystemCallerContext"/> 的 key.
    /// </summary>
    public const string CallerContextItemKey = "MoAI.SubsystemCallerContext";

    /// <summary>
    /// 构造子系统 token 的 audience.
    /// </summary>
    /// <param name="server">服务器地址（SystemOptions.Server）.</param>
    /// <returns>返回子系统 audience.</returns>
    public static string BuildAudience(string server) => $"{server}{AudienceSuffix}";
}
