using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Jina 网页阅读请求参数.
/// </summary>
public class JinaReaderRequest
{
    /// <summary>
    /// 要阅读的网页地址.
    /// </summary>
    [Description("要阅读的网页地址，必须是 http/https 开头的完整地址")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 正文返回格式.
    /// </summary>
    [Description("正文返回格式：markdown（默认）、text、html、screenshot、pageshot；填其它值时回落 markdown")]
    public string ReturnFormat { get; set; } = "markdown";

    /// <summary>
    /// 是否附带页面链接摘要.
    /// </summary>
    [Description("是否在正文后附带页面链接摘要（地址 → 锚文本），默认 false")]
    public bool WithLinksSummary { get; set; }

    /// <summary>
    /// 是否附带页面图片摘要.
    /// </summary>
    [Description("是否在正文后附带页面图片摘要（地址 → 说明），默认 false")]
    public bool WithImagesSummary { get; set; }

    /// <summary>
    /// 是否绕过缓存强制重新抓取.
    /// </summary>
    [Description("是否绕过缓存强制重新抓取，默认 false")]
    public bool NoCache { get; set; }

    /// <summary>
    /// 抓取超时秒数.
    /// </summary>
    [Description("抓取超时秒数，不填使用服务端默认值")]
    public int? Timeout { get; set; }
}
