namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 接入 token 换取限流（公网暴力枚举防护，Redis 固定窗口计数）：
/// 维度一 (客户端 IP + appid) 每窗口 10 次 / 维度二 客户端 IP 每窗口 50 次，
/// 仅对换取失败的 401 计数（停用 403、成功换取不计数）；
/// 任一维度超限后窗口内该维度换取直接 429，不再校验.
/// </summary>
public interface ISubsysTokenRateLimiter
{
    /// <summary>
    /// 提取客户端 IP：优先 X-Forwarded-For 首段（反代部署），否则 RemoteIpAddress.
    /// </summary>
    /// <returns>返回客户端 IP，取不到返回 unknown.</returns>
    string GetClientIp();

    /// <summary>
    /// 查询该 (IP, appid) 或 IP 维度是否已被限流.
    /// </summary>
    /// <param name="ip">客户端 IP.</param>
    /// <param name="appId">接入 appid.</param>
    /// <returns>已限流返回 true（调用方应返回 429）.</returns>
    bool IsBlocked(string ip, string appId);

    /// <summary>
    /// 记一次换取失败（401），两个维度计数均递增.
    /// </summary>
    /// <param name="ip">客户端 IP.</param>
    /// <param name="appId">接入 appid.</param>
    /// <returns>返回任务.</returns>
    Task CountFailureAsync(string ip, string appId);

    /// <summary>
    /// 清空全部限流计数（接入删除/重置密钥时调用，防止同名 appid 重建被残留计数误伤 60s 窗口）.
    /// </summary>
    /// <returns>返回任务.</returns>
    Task ResetAllAsync();
}
