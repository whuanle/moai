#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.FlowWait;

/// <summary>
/// 流程等待参数.
/// </summary>
public class FlowWaitParams
{
    /// <summary>
    /// 等待时间（秒）.
    /// </summary>
    [JsonPropertyName(nameof(WaitTimeInSeconds))]
    public int WaitTimeInSeconds { get; set; }
}