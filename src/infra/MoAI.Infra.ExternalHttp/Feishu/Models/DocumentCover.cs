#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 文档封面。
/// </summary>
public class DocumentCover
{
    /// <summary>
    /// 图片 token。
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; init; }

    /// <summary>
    /// 视图在水平方向的偏移比例。
    /// </summary>
    [JsonPropertyName("offset_ratio_x")]
    public float OffsetRatioX { get; init; }

    /// <summary>
    /// 视图在垂直方向的偏移比例。
    /// </summary>
    [JsonPropertyName("offset_ratio_y")]
    public float OffsetRatioY { get; init; }
}