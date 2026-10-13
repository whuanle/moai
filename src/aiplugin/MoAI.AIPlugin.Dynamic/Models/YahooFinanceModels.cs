using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 单只股票报价（quote 模式）.
/// </summary>
public class YahooFinanceQuote
{
    /// <summary>
    /// 股票代码.
    /// </summary>
    [Description("股票代码")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 公司/标的名称.
    /// </summary>
    [Description("公司或标的名称")]
    public string? Name { get; set; }

    /// <summary>
    /// 计价货币.
    /// </summary>
    [Description("计价货币，如 USD、CNY")]
    public string? Currency { get; set; }

    /// <summary>
    /// 交易所.
    /// </summary>
    [Description("交易所，如 NMS（纳斯达克）、SHZ（深交所）")]
    public string? Exchange { get; set; }

    /// <summary>
    /// 标的类型.
    /// </summary>
    [Description("标的类型，如 EQUITY、INDEX、CURRENCY")]
    public string? InstrumentType { get; set; }

    /// <summary>
    /// 最新价.
    /// </summary>
    [Description("最新价")]
    public double? Price { get; set; }

    /// <summary>
    /// 昨收价.
    /// </summary>
    [Description("昨收价")]
    public double? PreviousClose { get; set; }

    /// <summary>
    /// 涨跌额（最新价 - 昨收）.
    /// </summary>
    [Description("涨跌额（最新价 - 昨收）")]
    public double? ChangeAmount { get; set; }

    /// <summary>
    /// 涨跌幅（百分比，保留两位小数）.
    /// </summary>
    [Description("涨跌幅（百分比，保留两位小数）")]
    public double? ChangePercent { get; set; }

    /// <summary>
    /// 日内最高.
    /// </summary>
    [Description("日内最高")]
    public double? DayHigh { get; set; }

    /// <summary>
    /// 日内最低.
    /// </summary>
    [Description("日内最低")]
    public double? DayLow { get; set; }

    /// <summary>
    /// 成交量.
    /// </summary>
    [Description("成交量")]
    public long? Volume { get; set; }

    /// <summary>
    /// 52 周最高.
    /// </summary>
    [Description("52 周最高")]
    public double? FiftyTwoWeekHigh { get; set; }

    /// <summary>
    /// 52 周最低.
    /// </summary>
    [Description("52 周最低")]
    public double? FiftyTwoWeekLow { get; set; }

    /// <summary>
    /// 行情时间（UTC，ISO 8601）.
    /// </summary>
    [Description("行情时间（UTC，ISO 8601）")]
    public string? MarketTime { get; set; }

    /// <summary>
    /// 区间收盘价序列（IncludeHistory=true 时返回，最多最近 30 点）.
    /// </summary>
    [Description("区间收盘价序列（IncludeHistory=true 时返回，最多最近 30 点）")]
    public List<YahooFinanceHistoryPoint> History { get; set; } = [];
}

/// <summary>
/// 一条财经新闻（news 模式）.
/// </summary>
public class YahooFinanceNewsItem
{
    /// <summary>
    /// 新闻标题.
    /// </summary>
    [Description("新闻标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 发布方.
    /// </summary>
    [Description("发布方/媒体")]
    public string? Publisher { get; set; }

    /// <summary>
    /// 原文链接.
    /// </summary>
    [Description("原文链接")]
    public string? Link { get; set; }

    /// <summary>
    /// 发布时间（UTC，ISO 8601）.
    /// </summary>
    [Description("发布时间（UTC，ISO 8601）")]
    public string? PublishTime { get; set; }

    /// <summary>
    /// 相关股票代码.
    /// </summary>
    [Description("相关股票代码列表")]
    public List<string> RelatedTickers { get; set; } = [];
}

/// <summary>
/// 一条股票代码候选（search 模式）.
/// </summary>
public class YahooFinanceSymbolItem
{
    /// <summary>
    /// 股票代码.
    /// </summary>
    [Description("股票代码")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 名称.
    /// </summary>
    [Description("公司或标的名称")]
    public string? Name { get; set; }

    /// <summary>
    /// 交易所.
    /// </summary>
    [Description("交易所")]
    public string? Exchange { get; set; }

    /// <summary>
    /// 类型展示名.
    /// </summary>
    [Description("类型展示名，如 Equity、Index、Option")]
    public string? TypeDisplay { get; set; }

    /// <summary>
    /// 类型代码.
    /// </summary>
    [Description("类型代码，如 EQUITY、INDEX")]
    public string? QuoteType { get; set; }
}

/// <summary>
/// 一个行情采样点（收盘价）.
/// </summary>
public class YahooFinanceHistoryPoint
{
    /// <summary>
    /// 采样时间（UTC，ISO 8601）.
    /// </summary>
    [Description("采样时间（UTC，ISO 8601）")]
    public string? Time { get; set; }

    /// <summary>
    /// 收盘价.
    /// </summary>
    [Description("收盘价")]
    public double? Close { get; set; }
}

/// <summary>
/// 单代码失败记录（quote 模式，不影响其余代码）.
/// </summary>
public class YahooFinanceErrorItem
{
    /// <summary>
    /// 失败的股票代码.
    /// </summary>
    [Description("失败的股票代码")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 失败原因.
    /// </summary>
    [Description("失败原因")]
    public string Message { get; set; } = string.Empty;
}
