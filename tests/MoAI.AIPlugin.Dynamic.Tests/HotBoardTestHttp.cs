using System.Net;
using System.Text;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 热榜渠道测试桩：用委托按请求回放响应，捕获请求 URL 供断言.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpHandler"/> class.
    /// </summary>
    /// <param name="responder">按请求构造响应.</param>
    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>
    /// 已收到的请求地址（按序）.
    /// </summary>
    public List<string> RequestUris { get; } = [];

    /// <summary>
    /// 已收到的请求方法（按序，与 <see cref="RequestUris"/> 对齐）.
    /// </summary>
    public List<string> RequestMethods { get; } = [];

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
        RequestMethods.Add(request.Method.ToString());
        return Task.FromResult(_responder(request));
    }

    /// <summary>
    /// 构造 JSON 响应.
    /// </summary>
    /// <param name="payload">匿名对象载荷.</param>
    /// <returns>返回 200 响应.</returns>
    public static HttpResponseMessage Json(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>
    /// 构造文本响应（HTML/JS 变量壳）.
    /// </summary>
    /// <param name="body">正文.</param>
    /// <param name="mediaType">Content-Type，默认 text/html.</param>
    /// <returns>返回 200 响应.</returns>
    public static HttpResponseMessage Text(string body, string mediaType = "text/html")
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };
    }
}
