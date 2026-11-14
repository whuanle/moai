using System.Net;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 协作面 HTTP 桩：记录每个请求（method/uri/authorization/body），按注册规则返回响应。
/// </summary>
internal sealed class StubSubsysHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Responder> _routes = new();
    private readonly Dictionary<string, int> _invocations = new();

    /// <summary>响应规则：按请求与调用序号选择响应；未命中返回 404。</summary>
    /// <param name="request">捕获的请求快照。</param>
    /// <param name="invocation">同一路由的调用序号（0 起）。</param>
    /// <returns>桩响应。</returns>
    internal delegate HttpResponseMessage Responder(CapturedRequest request, int invocation);

    /// <summary>已捕获请求（按调用顺序）。</summary>
    public List<CapturedRequest> Requests { get; } = new();

    /// <summary>注册路由：路由键 = $"{method} {absolutePath}"。</summary>
    /// <param name="methodAndPath">路由键。</param>
    /// <param name="responder">响应回调。</param>
    /// <returns>桩自身（链式）。</returns>
    public StubSubsysHandler On(string methodAndPath, Func<CapturedRequest, HttpResponseMessage> responder)
    {
        _routes[methodAndPath] = (request, _) => responder(request);
        return this;
    }

    /// <summary>注册带调用序号的路由（用于首次 401 / 第二次 200 等序列）。</summary>
    /// <param name="methodAndPath">路由键。</param>
    /// <param name="responder">带调用序号的响应回调。</param>
    /// <returns>桩自身（链式）。</returns>
    public StubSubsysHandler OnByInvocation(string methodAndPath, Responder responder)
    {
        _routes[methodAndPath] = responder;
        return this;
    }

    /// <summary>取指定唯一请求的快照。</summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="absolutePath">绝对路径。</param>
    /// <returns>请求快照。</returns>
    public CapturedRequest Capture(string method, string absolutePath)
    {
        return Requests.Single(r => r.Method == method && r.Uri.AbsolutePath == absolutePath);
    }

    /// <summary>请求次数（两参形式）。</summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="absolutePath">绝对路径。</param>
    /// <returns>匹配次数。</returns>
    public int Count(string method, string absolutePath)
    {
        return Requests.Count(r => r.Method == method && r.Uri.AbsolutePath == absolutePath);
    }

    /// <summary>请求次数（"{method} {absolutePath}" 形式）。</summary>
    /// <param name="methodAndPath">路由键。</param>
    /// <returns>匹配次数。</returns>
    public int Count(string methodAndPath)
    {
        return Requests.Count(r => $"{r.Method} {r.Uri.AbsolutePath}" == methodAndPath);
    }

    internal static HttpResponseMessage Json(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var captured = await CapturedRequest.FromAsync(request);
        Requests.Add(captured);

        var key = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        var index = _invocations.TryGetValue(key, out var value) ? value : 0;
        _invocations[key] = index + 1;

        if (_routes.TryGetValue(key, out var responder))
        {
            return responder(captured, index);
        }

        var notFound = """{"error":{"type":"not_found","code":"no_route","message":"stub route missing"}}""";
        return Json(HttpStatusCode.NotFound, notFound);
    }
}
