namespace MoAI.Settings.Models;

/// <summary>
/// 资源使用统计落库粒度.
/// </summary>
public enum ResourceUsageGranularity
{
    /// <summary>
    /// 按天（默认，数据量小）.
    /// </summary>
    Day = 0,

    /// <summary>
    /// 按小时（更细，数据量约为按天的 24 倍）.
    /// </summary>
    Hour = 1,
}
