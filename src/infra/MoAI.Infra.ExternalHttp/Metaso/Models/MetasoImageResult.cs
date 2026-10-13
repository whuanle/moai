#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Metaso.Models;

/// <summary>
/// 秘塔搜索 API 单条图片结果.
/// </summary>
public class MetasoImageResult
{
    /// <summary>
    /// 图片标题.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 图片地址.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图地址.
    /// </summary>
    public string? Thumbnail { get; init; }
}
