namespace MoAI.Subsystem.Services;

/// <summary>
/// 系统应用接入（/api/subsys/system/*）协作面认证常量：Bearer key（moai-sa- 前缀）直连鉴权.
/// </summary>
public static class SystemAccessAppAuthDefaults
{
    /// <summary>
    /// key 前缀（协作文档与鉴权提示用）.
    /// </summary>
    public const string KeyPrefix = "moai-sa-";

    /// <summary>
    /// HttpContext.Items 中存放 <see cref="Models.SystemAccessAppCallerContext"/> 的 key.
    /// </summary>
    public const string CallerContextItemKey = "MoAI.SystemAccessAppCallerContext";
}
