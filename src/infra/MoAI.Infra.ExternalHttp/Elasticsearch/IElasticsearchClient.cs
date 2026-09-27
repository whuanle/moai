using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace MoAI.Infra.Elasticsearch;

/// <summary>
/// Elasticsearch HTTP 接口客户端接口（只读检索），服务地址来自实例配置，每次执行前按配置设置 BaseAddress.
/// </summary>
/// <remarks>index 路径参数支持通配符与逗号分隔多索引；全库检索统一用 <c>_all</c>.</remarks>
public interface IElasticsearchClient
{
    /// <summary>
    /// HttpClient.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// DSL 搜索（GET 语义的 POST /{index}/_search，请求体为用户提供的查询 DSL JSON）.
    /// </summary>
    /// <param name="authorization">Authorization 头（ApiKey / Basic，未配置鉴权时传 null）.</param>
    /// <param name="index">索引名（支持通配符，全库用 _all）.</param>
    /// <param name="dsl">查询 DSL（JSON 对象）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Post("/{index}/_search")]
    Task<string> SearchAsync([Header("Authorization")] string? authorization, string index, [Body] JsonElement dsl, CancellationToken cancellationToken = default);

    /// <summary>
    /// DSL 计数.
    /// </summary>
    /// <param name="authorization">Authorization 头（ApiKey / Basic，未配置鉴权时传 null）.</param>
    /// <param name="index">索引名（支持通配符，全库用 _all）.</param>
    /// <param name="dsl">查询 DSL（JSON 对象）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Post("/{index}/_count")]
    Task<string> CountAsync([Header("Authorization")] string? authorization, string index, [Body] JsonElement dsl, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取索引字段映射.
    /// </summary>
    /// <param name="authorization">Authorization 头（ApiKey / Basic，未配置鉴权时传 null）.</param>
    /// <param name="index">索引名（支持通配符，全库用 _all）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/{index}/_mapping")]
    Task<string> MappingAsync([Header("Authorization")] string? authorization, string index, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出索引（_cat/indices, JSON 列裁剪）.
    /// </summary>
    /// <param name="authorization">Authorization 头（ApiKey / Basic，未配置鉴权时传 null）.</param>
    /// <param name="format">输出格式（固定 json）.</param>
    /// <param name="headers">列裁剪（h=health,status,index,uuid,pri,rep,docs.count,store.size）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应 JSON 文本.</returns>
    [Get("/_cat/indices")]
    Task<string> CatIndicesAsync([Header("Authorization")] string? authorization, [Query, AliasAs("format")] string format, [Query, AliasAs("h")] string headers, CancellationToken cancellationToken = default);
}

