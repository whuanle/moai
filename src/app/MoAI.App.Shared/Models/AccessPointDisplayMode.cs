using System.Text.Json.Serialization;

namespace MoAI.App.Models;

/// <summary>
/// 访问点显示形式.
/// </summary>
public enum AccessPointDisplayMode
{
    /// <summary>
    /// 悬浮组件（右下/左下角悬浮按钮 + 弹出面板）.
    /// </summary>
    [JsonPropertyName("floating")]
    Floating = 0,

    /// <summary>
    /// 内嵌小窗（宿主页放置占位容器，面板嵌入页面，可放大为近全屏浮层）.
    /// </summary>
    [JsonPropertyName("inline")]
    Inline = 1,
}
