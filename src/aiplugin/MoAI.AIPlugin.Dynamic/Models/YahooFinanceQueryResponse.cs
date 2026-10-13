using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 雅虎财经响应结果：按 Mode 只填对应集合.
/// </summary>
public class YahooFinanceQueryResponse
{
    /// <summary>
    /// 本次运行的模式.
    /// </summary>
    [System.ComponentModel.Description("本次运行的模式：quote | news | search")]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// quote 模式：逐代码报价.
    /// </summary>
    [System.ComponentModel.Description("quote 模式：逐代码报价列表")]
    public IReadOnlyList<YahooFinanceQuote> Quotes { get; init; } = [];

    /// <summary>
    /// news 模式：新闻列表.
    /// </summary>
    [System.ComponentModel.Description("news 模式：新闻列表")]
    public IReadOnlyList<YahooFinanceNewsItem> News { get; init; } = [];

    /// <summary>
    /// search 模式：股票代码候选.
    /// </summary>
    [System.ComponentModel.Description("search 模式：股票代码候选列表")]
    public IReadOnlyList<YahooFinanceSymbolItem> Candidates { get; init; } = [];

    /// <summary>
    /// quote 模式：单代码失败清单（其余代码继续返回）.
    /// </summary>
    [System.ComponentModel.Description("quote 模式：单代码失败清单（其余代码继续返回）")]
    public IReadOnlyList<YahooFinanceErrorItem> Errors { get; init; } = [];

    /// <summary>
    /// 结果是否因超过实例配置上限被截断.
    /// </summary>
    [System.ComponentModel.Description("结果是否因超过实例配置上限被截断")]
    public bool Truncated { get; set; }
}
