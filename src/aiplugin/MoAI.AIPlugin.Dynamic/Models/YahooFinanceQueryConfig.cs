using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 雅虎财经插件配置（每个实例独立保存）.
/// </summary>
public class YahooFinanceQueryConfig
{
    /// <summary>
    /// 数据源地址，留空使用雅虎财经官方地址.
    /// </summary>
    [Description("数据源地址，留空使用官方 https://query1.finance.yahoo.com；可指向镜像、代理或兼容桩服务")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数.
    /// </summary>
    [Description("单次请求超时秒数，1-120，默认 20")]
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// quote 模式单次最多解析的代码数.
    /// </summary>
    [Description("quote 模式单次最多解析的代码数，1-50，默认 10")]
    public int MaxSymbols { get; set; } = 10;

    /// <summary>
    /// news 模式最多返回的新闻条数.
    /// </summary>
    [Description("news 模式最多返回的新闻条数，1-50，默认 10")]
    public int MaxNews { get; set; } = 10;

    /// <summary>
    /// search 模式最多返回的候选条数.
    /// </summary>
    [Description("search 模式最多返回的候选条数，1-50，默认 10")]
    public int MaxSearchResults { get; set; } = 10;

    /// <summary>
    /// 默认请求语言.
    /// </summary>
    [Description("默认请求语言（如 zh-CN、en-US），留空不传")]
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// 默认市场区域.
    /// </summary>
    [Description("默认市场区域（如 US、CN），留空不传")]
    public string Region { get; set; } = string.Empty;
}
