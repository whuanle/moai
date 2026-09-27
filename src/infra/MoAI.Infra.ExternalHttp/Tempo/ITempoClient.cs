using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace MoAI.Infra.Tempo;

/// <summary>
/// Grafana Tempo HTTP API 客户端接口（只读查询），服务地址来自实例配置，每次执行前按配置设置 BaseAddress.
/// </summary>
public interface ITempoClient
{
    /// <summary>
    /// HttpClient.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// TraceQL 链路检索.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="organizationId">Grafana 多租户头的租户 id（x-scope-orgid，可空）.</param>
    /// <param name="traceql">TraceQL 表达式.</param>
    /// <param name="limit">最多返回链路数.</param>
    /// <param name="start">开始时间（RFC3339 / Unix 秒，可空）.</param>
    /// <param name="end">结束时间（RFC3339 / Unix 秒，可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/search")]
    Task<string> SearchAsync([Header("Authorization")] string? authorization, [Header("x-scope-orgid")] string? organizationId, [Query, AliasAs("query")] string traceql, [Query, AliasAs("limit")] int limit, [Query, AliasAs("start")] string? start, [Query, AliasAs("end")] string? end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 traceID 获取链路详情（Accept: application/json 用于取 JSON 形态，缺省会返回 protobuf）.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="organizationId">Grafana 多租户头的租户 id（x-scope-orgid，可空）.</param>
    /// <param name="accept">Accept 头值（传 application/json）.</param>
    /// <param name="traceId">TraceID.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/traces/{traceID}")]
    Task<string> GetTraceAsync([Header("Authorization")] string? authorization, [Header("x-scope-orgid")] string? organizationId, [Header("Accept")] string accept, string traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可检索的标签名列表.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="organizationId">Grafana 多租户头的租户 id（x-scope-orgid，可空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/search/tags")]
    Task<string> TagsAsync([Header("Authorization")] string? authorization, [Header("x-scope-orgid")] string? organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取某个标签的取值列表.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic / Bearer，未配置鉴权时传 null）.</param>
    /// <param name="organizationId">Grafana 多租户头的租户 id（x-scope-orgid，可空）.</param>
    /// <param name="tagName">标签名（如 service.name）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/api/search/tag/{tagName}/values")]
    Task<string> TagValuesAsync([Header("Authorization")] string? authorization, [Header("x-scope-orgid")] string? organizationId, string tagName, CancellationToken cancellationToken = default);
}

