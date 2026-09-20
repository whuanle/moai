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
}
