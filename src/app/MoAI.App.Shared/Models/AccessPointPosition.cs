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

/// <summary>
/// 访问点位置文本归一化：库存 kebab（bottom-right）与历史 camel（bottomRight）统一为 camel，
/// 悬浮组件契约只认 camel（存量库两代拼写并存，归一化在读取侧完成，不回写库）.
/// </summary>
public static class AccessPointPositionText
{
    /// <summary>
    /// 归一化为 bottomRight / bottomLeft，空白与未知值回退 bottomRight.
    /// </summary>
    /// <param name="stored">库存原始值.</param>
    /// <returns>camelCase 位置.</returns>
    public static string Normalize(string? stored)
    {
        return stored is "bottom-left" or "bottomLeft" ? "bottomLeft" : "bottomRight";
    }
}

/// <summary>
/// 访问点显示形式文本归一化：库存 camel（floating/inline），未知值回退 floating.
/// </summary>
public static class AccessPointDisplayModeText
{
    /// <summary>
    /// 归一化为 floating / inline.
    /// </summary>
    /// <param name="stored">库存原始值.</param>
    /// <returns>camelCase 显示形式.</returns>
    public static string Normalize(string? stored)
    {
        return stored == "inline" ? "inline" : "floating";
    }
}
