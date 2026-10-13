using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 秘塔网页阅读请求参数.
/// </summary>
public class MetasoReaderRequest
{
    /// <summary>
    /// 要阅读的网页地址.
    /// </summary>
    [Description("要阅读的网页地址，http/https 开头的完整地址")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 返回格式.
    /// </summary>
    [Description("返回格式：markdown（默认，返回 Markdown 正文）、json（额外解析标题/作者/日期等结构化字段）；填其它值时回落 markdown")]
    public string Format { get; set; } = "markdown";
}
