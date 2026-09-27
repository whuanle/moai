using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace MoAI.Infra.Prometheus;

/// <summary>
/// Prometheus HTTP API v1 客户端接口（只读查询），服务地址来自实例配置，每次执行前按配置设置 BaseAddress.
/// </summary>
public interface IPrometheusClient
{
    /// <summary>
    /// HttpClient.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// 即时查询（单点 PromQL 求值）.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="query">PromQL 表达式.</param>
    /// <param name="time">求值时间（RFC3339 或 Unix 秒），缺省当前时间.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/query")]
    Task<string> QueryAsync([Header("Authorization")] string? authorization, [Query, AliasAs("query")] string query, [Query, AliasAs("time")] string? time, CancellationToken cancellationToken = default);

    /// <summary>
    /// 区间查询（按步长求值时间序列）.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="query">PromQL 表达式.</param>
    /// <param name="start">开始时间（RFC3339 或 Unix 秒）.</param>
    /// <param name="end">结束时间（RFC3339 或 Unix 秒）.</param>
    /// <param name="step">步长（持续时间字面量，如 1m、5m）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/query_range")]
    Task<string> QueryRangeAsync([Header("Authorization")] string? authorization, [Query, AliasAs("query")] string query, [Query, AliasAs("start")] string start, [Query, AliasAs("end")] string end, [Query, AliasAs("step")] string step, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询标签名列表.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/labels")]
    Task<string> LabelsAsync([Header("Authorization")] string? authorization, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询某个标签的全部取值.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="name">标签名.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/label/{name}/values")]
    Task<string> LabelValuesAsync([Header("Authorization")] string? authorization, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按选择器查询时间序列的标签集.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="match">series 选择器（match[]），支持逗号分隔多个.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/series")]
    Task<string> SeriesAsync([Header("Authorization")] string? authorization, [Query, AliasAs("match[]")] string? match, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询当前触发的告警.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/alerts")]
    Task<string> AlertsAsync([Header("Authorization")] string? authorization, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询告警与记录规则组.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/v1/rules")]
    Task<string> RulesAsync([Header("Authorization")] string? authorization, CancellationToken cancellationToken = default);
}

