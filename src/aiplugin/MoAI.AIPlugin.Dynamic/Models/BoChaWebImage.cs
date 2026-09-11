using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查全网搜索的单条图片结果.
/// </summary>
public class BoChaWebImage
{
    /// <summary>
    /// 图片名称.
    /// </summary>
    [Description("图片名称")]
    public string? Name { get; set; }

    /// <summary>
    /// 图片缩略图 URL.
    /// </summary>
    [Description("图片缩略图 URL")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// 原图 URL.
    /// </summary>
    [Description("原图 URL")]
    public string? ContentUrl { get; set; }

    /// <summary>
    /// 图片所在网页的 URL.
    /// </summary>
    [Description("图片所在网页的 URL")]
    public string? HostPageUrl { get; set; }

    /// <summary>
    /// 原图宽度（像素）.
    /// </summary>
    [Description("原图宽度（像素）")]
    public int? Width { get; set; }

    /// <summary>
    /// 原图高度（像素）.
    /// </summary>
    [Description("原图高度（像素）")]
    public int? Height { get; set; }
}
