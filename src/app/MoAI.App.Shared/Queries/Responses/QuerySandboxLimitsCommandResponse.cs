namespace MoAI.App.Queries.Responses;

/// <summary>
/// 沙箱资源上限.
/// </summary>
public class QuerySandboxLimitsCommandResponse
{
    /// <summary>
    /// 每个应用沙箱最大存活时间（秒）.
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
    /// 出站网络管控模式：app（应用自定义）/ deny（强制默认拒绝）/ intranet（默认拒绝并放行内网网段）.
    /// </summary>
    public string NetworkMode { get; init; } = "app";

    /// <summary>
    /// 内网网段（IP / CIDR，逗号分隔原文），intranet 模式下运行时自动放行.
    /// </summary>
    public string IntranetCidrs { get; init; } = string.Empty;

    /// <summary>
    /// 平台级禁用命令清单（换行分隔模式原文，全站强制）；空表示未启用.
    /// </summary>
    public string DeniedCommands { get; init; } = string.Empty;
}
