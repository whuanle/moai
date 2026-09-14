using System.Text.Json.Serialization;

namespace MoAI.App.Models;

/// <summary>
/// 访问点悬浮位置.
/// </summary>
public enum AccessPointPosition
{
    /// <summary>
    /// 右下角.
    /// </summary>
    [JsonPropertyName("bottom-right")]
    BottomRight = 0,

    /// <summary>
    /// 左下角.
    /// </summary>
    [JsonPropertyName("bottom-left")]
    BottomLeft = 1,
}
