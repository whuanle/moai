namespace MoAI.Settings.Models;

/// <summary>
/// 沙箱资源上限（供业务模块读取，由超级管理员在系统设置页调整）.
/// </summary>
public class SandboxLimitsSettings
{
    /// <summary>
    /// 每个应用沙箱最大存活时间（秒），下限 60.
    /// </summary>
    public int MaxTtlSeconds { get; init; }

    /// <summary>
    /// 每个应用沙箱 CPU 限制上限（K8s 数量格式原文，如 "4"、"2000m"）.
    /// </summary>
    public string MaxCpu { get; init; } = string.Empty;

    /// <summary>
    /// 每个应用沙箱内存限制上限（K8s 数量格式原文，如 "8Gi"、"512Mi"）.
    /// </summary>
    public string MaxMemory { get; init; } = string.Empty;

    /// <summary>
    /// CPU 上限换算的毫核数（1 核 = 1000m），用于比较.
    /// </summary>
    public long MaxCpuMillicores { get; init; }

    /// <summary>
    /// 内存上限换算的字节数，用于比较.
    /// </summary>
    public long MaxMemoryBytes { get; init; }

    /// <summary>
    /// 出站网络管控模式：app（应用自定义）/ deny（强制默认拒绝）/ intranet（默认拒绝并放行内网网段）.
    /// </summary>
    public string NetworkMode { get; init; } = NetworkModes.App;

    /// <summary>
    /// 内网网段列表（IP / CIDR），intranet 模式下自动放行.
    /// </summary>
    public IReadOnlyList<string> IntranetCidrs { get; init; } = [];

    /// <summary>
    /// 平台级禁用命令清单（全站强制，与应用级黑名单取并集）；空清单表示平台级未启用.
    /// </summary>
    public IReadOnlyList<string> DeniedCommands { get; init; } = [];
}

/// <summary>
/// 沙箱出站网络管控模式常量.
/// </summary>
public static class NetworkModes
{
    /// <summary>
    /// 应用自定义（默认）：应用配置的出站策略原样生效.
    /// </summary>
    public const string App = "app";

    /// <summary>
    /// 强制默认拒绝：defaultAction 一律按 deny 下发，应用规则中的放行条目仍生效.
    /// </summary>
    public const string Deny = "deny";

    /// <summary>
    /// 仅内网优先：默认拒绝并自动放行内网网段，应用可追加白名单目标.
    /// </summary>
    public const string Intranet = "intranet";

    /// <summary>
    /// 是否合法的模式值.
    /// </summary>
    /// <param name="value">模式原文.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValid(string? value)
        => value?.Trim().ToLowerInvariant() is App or Deny or Intranet;
}
