using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔搜索响应结果.
/// </summary>
public class MetasoSearchResponse
{
    /// <summary>
    /// 实际执行的搜索词.
    /// </summary>
    [Description("实际执行的搜索词")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 实际使用的搜索范围.
    /// </summary>
    [Description("实际使用的搜索范围：webpage/document/paper/image/video/podcast")]
    public string Scope { get; set; } = "webpage";

    /// <summary>
    /// 搜索结果列表（网页/文库/视频统一形态）.
    /// </summary>
    [Description("搜索结果列表（网页/文库/视频范围返回），每条含标题、链接、摘要等")]
    public IReadOnlyList<MetasoSearchPage> Results { get; init; } = [];

    /// <summary>
    /// 图片结果列表.
    /// </summary>
    [Description("图片结果列表（image 范围返回），每条含标题、图片地址、缩略图地址")]
    public IReadOnlyList<MetasoSearchImage> Images { get; init; } = [];

    /// <summary>
    /// 命中结果总数.
    /// </summary>
    [Description("命中结果总数")]
    public int? Total { get; set; }

    /// <summary>
    /// 综合摘要.
    /// </summary>
    [Description("综合摘要（请求 IncludeSummary=true 时才有）")]
    public string? Summary { get; set; }

    /// <summary>
    /// 本次调用消耗的点数.
    /// </summary>
    [Description("本次调用消耗的点数")]
    public int? Credits { get; set; }
}
