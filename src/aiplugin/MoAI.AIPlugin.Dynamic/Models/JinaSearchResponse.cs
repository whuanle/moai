using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Jina 搜索响应结果.
/// </summary>
public class JinaSearchResponse
{
    /// <summary>
    /// 实际执行的搜索词.
    /// </summary>
    [Description("实际执行的搜索词")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 搜索结果列表.
    /// </summary>
    [Description("搜索结果列表，每条含标题、链接、摘要与页面正文")]
    public IReadOnlyList<JinaSearchPage> Results { get; init; } = [];

    /// <summary>
    /// 本次搜索消耗的 token 总数.
    /// </summary>
    [Description("本次搜索消耗的 token 总数")]
    public int? UsageTokens { get; set; }
}
