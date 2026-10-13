using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔网页阅读响应结果.
/// </summary>
public class MetasoReaderResponse
{
    /// <summary>
    /// 页面地址.
    /// </summary>
    [Description("页面地址（json 格式才有，markdown 格式回显请求地址）")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 页面标题.
    /// </summary>
    [Description("页面标题（json 格式才有）")]
    public string? Title { get; set; }

    /// <summary>
    /// 作者.
    /// </summary>
    [Description("作者（json 格式才有）")]
    public string? Author { get; set; }

    /// <summary>
    /// 发布日期.
    /// </summary>
    [Description("发布日期（json 格式才有）")]
    public string? Date { get; set; }

    /// <summary>
    /// 正文.
    /// </summary>
    [Description("页面正文（Markdown）")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 本次调用消耗的点数.
    /// </summary>
    [Description("本次调用消耗的点数（json 格式才有）")]
    public int? Credits { get; set; }
}
