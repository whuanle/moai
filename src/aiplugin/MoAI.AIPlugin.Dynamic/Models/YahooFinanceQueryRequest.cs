using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 雅虎财经请求参数.
/// </summary>
public class YahooFinanceQueryRequest
{
    /// <summary>
    /// 查询模式.
    /// </summary>
    [Description("查询模式：quote（股票报价）、news（财经新闻）、search（股票代码搜索）；留空默认 quote")]
    public string Mode { get; set; } = "quote";

    /// <summary>
    /// quote 模式的股票代码列表.
    /// </summary>
    [Description("quote 模式必填：股票代码，多个用英文逗号分隔，如 AAPL,MSFT,BRK-B,000001.SS")]
    public string Symbols { get; set; } = string.Empty;

    /// <summary>
    /// news/search 模式的关键词.
    /// </summary>
    [Description("news/search 模式必填：关键词或股票代码，如 苹果、AI 芯片、AAPL")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// news 模式返回的新闻条数.
    /// </summary>
    [Description("news 模式：返回新闻条数，留空取实例配置上限")]
    public int? NewsCount { get; set; }

    /// <summary>
    /// search 模式返回的候选条数.
    /// </summary>
    [Description("search 模式：返回候选条数，留空取实例配置上限")]
    public int? SearchCount { get; set; }

    /// <summary>
    /// 行情区间.
    /// </summary>
    [Description("quote 模式 IncludeHistory=true 时的行情区间：1d(默认)|5d|1mo|3mo|6mo|1y|2y|5y|10y|ytd|max")]
    public string Range { get; set; } = "1d";

    /// <summary>
    /// 采样粒度.
    /// </summary>
    [Description("quote 模式 IncludeHistory=true 时的采样粒度：1m|2m|5m|15m|30m|60m|90m|1h|1d(默认)|5d|1wk|1mo|3mo")]
    public string Interval { get; set; } = "1d";

    /// <summary>
    /// 是否附带区间收盘价序列.
    /// </summary>
    [Description("quote 模式是否附带区间收盘价序列（最多保留最近 30 点），默认 false")]
    public bool IncludeHistory { get; set; }

    /// <summary>
    /// 覆盖实例默认的请求语言.
    /// </summary>
    [Description("覆盖实例默认语言的请求语言（如 zh-CN），留空用实例配置")]
    public string? Lang { get; set; }

    /// <summary>
    /// 覆盖实例默认的市场区域.
    /// </summary>
    [Description("覆盖实例默认市场区域（如 US），留空用实例配置")]
    public string? Region { get; set; }
}
