using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 博查全网搜索响应结果.
/// </summary>
public class BoChaWebSearchResponse
{
    /// <summary>
    /// 原始的搜索关键字.
    /// </summary>
    [Description("原始的搜索关键字")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 搜索匹配的网页总数（估算值）.
    /// </summary>
    [Description("搜索匹配的网页总数（估算值）")]
    public int TotalEstimatedMatches { get; set; }

    /// <summary>
    /// 结果中是否有被安全过滤的内容.
    /// </summary>
    [Description("结果中是否有被安全过滤的内容")]
    public bool SomeResultsRemoved { get; set; }

    /// <summary>
    /// 网页搜索结果.
    /// </summary>
    [Description("网页搜索结果列表")]
    public IReadOnlyList<BoChaWebPage> WebPages { get; init; } = [];

    /// <summary>
    /// 图片搜索结果.
    /// </summary>
    [Description("图片搜索结果列表")]
    public IReadOnlyList<BoChaWebImage> Images { get; init; } = [];
}
