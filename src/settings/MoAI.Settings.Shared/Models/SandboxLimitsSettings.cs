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
}
