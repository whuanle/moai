using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace MoAI.Infra.YahooFinance;

/// <summary>
/// 雅虎财经公开数据接口客户端（只读），服务地址来自动态插件实例配置，每次执行前按配置设置 BaseAddress.
/// </summary>
public interface IYahooFinanceClient
{
    /// <summary>
    /// HttpClient.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// 个股行情（v8 chart）：meta 含最新价、昨收、日内高低、成交量、52 周高低等，indicators 含区间采样点.
    /// </summary>
    /// <param name="symbol">股票代码，如 AAPL、BRK-B、000001.SS.</param>
    /// <param name="range">行情区间（1d/5d/1mo/.../max），缺省由上游决定.</param>
    /// <param name="interval">采样粒度（1m/1d/1wk/...），缺省由上游决定.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/v8/finance/chart/{symbol}")]
    Task<string> ChartAsync(string symbol, [Query] string? range, [Query] string? interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// 代码搜索与财经新闻（v1 search）：q 命中股票代码候选（quotes）与相关新闻（news）.
    /// </summary>
    /// <param name="q">关键词或股票代码.</param>
    /// <param name="quotesCount">返回代码候选条数，0 表示不返回.</param>
    /// <param name="newsCount">返回新闻条数，0 表示不返回.</param>
    /// <param name="lang">语言（如 zh-CN），null 表示不传.</param>
    /// <param name="region">市场区域（如 US），null 表示不传.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/v1/finance/search")]
    Task<string> SearchAsync([Query, AliasAs("q")] string q, [Query, AliasAs("quotesCount")] int? quotesCount, [Query, AliasAs("newsCount")] int? newsCount, [Query] string? lang, [Query] string? region, CancellationToken cancellationToken = default);
}
