using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Jina 搜索请求参数.
/// </summary>
public class JinaSearchRequest
{
    /// <summary>
    /// 用户的搜索词.
    /// </summary>
    [Description("用户的搜索词，可为关键词或自然语言问题")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 命中页面正文返回格式.
    /// </summary>
    [Description("命中页面正文返回格式：markdown（默认）、text、html、screenshot、pageshot；填其它值时回落 markdown")]
    public string ReturnFormat { get; set; } = "markdown";

    /// <summary>
    /// 是否绕过缓存强制重新抓取.
    /// </summary>
    [Description("是否绕过缓存强制重新抓取，默认 false")]
    public bool NoCache { get; set; }
}
