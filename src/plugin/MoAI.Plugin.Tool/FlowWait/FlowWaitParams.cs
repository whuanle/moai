#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using MoAI.Plugin.Attributes;
using MoAI.Plugin.Plugins;
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
    [NativePluginField(
        Key = nameof(WaitTimeInSeconds),
        Description = "等待时间（秒）",
        FieldType = PluginConfigFieldType.Number,
        IsRequired = true,
        ExampleValue = "10")]
    public int WaitTimeInSeconds { get; set; }
}