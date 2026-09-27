using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace MoAI.Infra.ClickHouse;

/// <summary>
/// ClickHouse HTTP 接口客户端接口（只读查询），服务地址来自实例配置，每次执行前按配置设置 BaseAddress.
/// </summary>
/// <remarks>
/// 固定走 JSONEachRow 出参：查询表达式经 URL <c>?query=</c> 下发（Refit 对查询参数自动转义），
/// 并强制 <c>readonly=1</c>、行数上限（<c>max_result_rows</c>+<c>result_overflow_mode=break</c>）、
/// 执行超时（<c>max_execution_time</c>）；Int64 不加引号、NaN/Inf 加引号，避免产出非法 JSON。
/// 参数走 ClickHouse HTTP <c>param_&lt;name&gt; → $name</c> 绑定，键名由调用方带齐 <c>param_</c> 前缀。
/// </remarks>
public interface IClickHouseClient
{
    /// <summary>
    /// HttpClient.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// POST / 执行查询：表达式置于 <c>?query=</c>，结果为逐行 JSONEachRow.
    /// </summary>
    /// <param name="authorization">Authorization 头（Basic，未配置鉴权时传 null）.</param>
    /// <param name="query">查询表达式（URL 编码由 Refit 完成）.</param>
    /// <param name="database">默认数据库（实例配置，可空）.</param>
    /// <param name="timeoutSeconds">服务端 max_execution_time（秒）.</param>
    /// <param name="maxResultRows">服务端行数上限（max_result_rows）.</param>
    /// <param name="readOnly">固定 1：服务端拒绝数据变更.</param>
    /// <param name="quote64bitIntegers">固定 0：Int64 序列化为裸数字.</param>
    /// <param name="quoteDenormals">固定 1：NaN/+Inf/-Inf 序列化为带引号文本，保证响应是合法 JSON.</param>
    /// <param name="defaultFormat">固定 JSONEachRow.</param>
    /// <param name="overflowMode">固定 break：达到行数上限时中断执行.</param>
    /// <param name="parameters">$name 参数绑定（键=param_*，null 被跳过）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>逐行 JSONEachRow 文本.</returns>
    [Post("/")]
    Task<string> QueryAsync([Header("Authorization")] string? authorization, [Query, AliasAs("query")] string query, [Query, AliasAs("database")] string? database, [Query, AliasAs("max_execution_time")] string timeoutSeconds, [Query, AliasAs("max_result_rows")] int maxResultRows, [Query, AliasAs("readonly")] int readOnly, [Query, AliasAs("output_format_json_quote_64bit_integers")] int quote64bitIntegers, [Query, AliasAs("output_format_json_quote_denormals")] int quoteDenormals, [Query, AliasAs("default_format")] string defaultFormat, [Query, AliasAs("result_overflow_mode")] string overflowMode, [Query] IReadOnlyDictionary<string, string?>? parameters, CancellationToken cancellationToken = default);
}

