using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔搜索单条图片结果.
/// </summary>
public class MetasoSearchImage
{
    /// <summary>
    /// 图片标题.
    /// </summary>
    [Description("图片标题")]
    public string? Title { get; set; }

    /// <summary>
    /// 图片地址.
    /// </summary>
    [Description("图片地址")]
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图地址.
    /// </summary>
    [Description("缩略图地址")]
    public string? Thumbnail { get; set; }
}
