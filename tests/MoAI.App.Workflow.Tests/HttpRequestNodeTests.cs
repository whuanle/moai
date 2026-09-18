using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Nodes.Builtin;
using Moq;
using Xunit;

namespace MoAI.App.Workflow.Tests;

/// <summary>
/// HTTP 请求节点测试：请求组装（方法/查询参数/请求头/鉴权/请求体）、响应解析与字段提取、
/// 报错捕获语义与定义校验（WorkflowValidator 集成）.
/// </summary>
public class HttpRequestNodeTests
{
    /// <summary>
    /// 桩 HttpMessageHandler：记录最近一次请求，按委托返回响应.
    /// </summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return await _responder(request, cancellationToken);
        }
    }

    private static StubHttpHandler JsonHandler(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));
    }

    private static NodeExecutionContext BuildContext(
        JsonObject config,
        WorkflowVariableScope? scope = null,
        JsonObject? inputs = null)
    {
        var node = new NodeDefinition
        {
            Key = "http1",
            Name = "HTTP 请求",
            Type = NodeTypes.Http,
            Config = JsonSerializer.SerializeToElement(config),
        };

        return new NodeExecutionContext(
            "inst-1",
            node,
            inputs ?? new JsonObject(),
            scope ?? new WorkflowVariableScope(),
            Mock.Of<IWorkflowEventPublisher>());
    }

    private static HttpRequestNodeExecutor BuildExecutor(HttpMessageHandler handler)
    {
        return new HttpRequestNodeExecutor(new ExpressionEvaluator(), handler);
    }

    private static WorkflowVariableScope CreateScope()
    {
        return new WorkflowVariableScope(
            systemVariables: new JsonObject { ["instanceId"] = "inst-1" },
            inputParameters: new JsonObject { ["word"] = "hello" },
            nodeOutputs: new Dictionary<string, JsonObject>
            {
                ["start"] = new JsonObject { ["word"] = "hello", ["count"] = 5 },
            });
    }

    [Fact]
    public async Task ExecuteAsync_GetWithParamsHeadersAuth_BuildsRequestAndExtractsFields()
    {
        var handler = JsonHandler("""{"data":{"title":"hi"},"list":[1,2]}""");
        var executor = BuildExecutor(handler);
        var context = BuildContext(new JsonObject
        {
            ["method"] = "GET",
            ["url"] = "http://example.com/api?from=cfg",
            ["params"] = new JsonArray
            {
                new JsonObject { ["name"] = "q", ["value"] = "{start.word}" },
            },
            ["headers"] = new JsonArray
            {
                new JsonObject { ["name"] = "X-Trace", ["value"] = "t-{start.word}" },
            },
            ["auth"] = new JsonObject { ["type"] = "bearer", ["token"] = "tk-{start.word}" },
            ["extract"] = new JsonArray
            {
                new JsonObject { ["name"] = "title", ["path"] = "$.data.title", ["fieldType"] = "string" },
                new JsonObject { ["name"] = "first", ["path"] = "$.list[0]", ["fieldType"] = "number" },
                new JsonObject { ["name"] = "all", ["path"] = "$.list[*]", ["fieldType"] = "array" },
            },
        }, CreateScope());

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("from=cfg", query);
        Assert.Contains("q=hello", query);
        Assert.Equal("t-hello", handler.LastRequest.Headers.TryGetValues("X-Trace", out var trace) ? trace.First() : null);
        Assert.Equal("Bearer tk-hello", handler.LastRequest.Headers.Authorization?.ToString());

        Assert.False((bool)result.Output["hasError"]!);
        Assert.Equal(200, (int)result.Output["statusCode"]!);
        Assert.Equal("hi", (string?)result.Output["title"]);
        Assert.Equal(1, (int)result.Output["first"]!);
        Assert.Equal(2, ((JsonArray)result.Output["all"]!).Count);
        Assert.Null((string?)result.Output["errorMessage"]);
        Assert.NotNull(result.Output["rawResponse"]);
    }

    [Fact]
    public async Task ExecuteAsync_PostJsonBody_SendsInterpolatedBody()
    {
        var handler = JsonHandler("""{"ok":true}""");
        var executor = BuildExecutor(handler);
        var context = BuildContext(new JsonObject
        {
            ["method"] = "POST",
            ["url"] = "http://example.com/create",
            ["bodyType"] = "json",
            ["body"] = """{"word":"{start.word}","count":{start.count}}""",
        }, CreateScope());

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.Equal("""{"word":"hello","count":5}""", handler.LastBody);
        Assert.StartsWith("application/json", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ExecuteAsync_FormBody_SendsUrlEncoded()
    {
        var handler = JsonHandler("""{"ok":true}""");
        var executor = BuildExecutor(handler);
        var context = BuildContext(new JsonObject
        {
            ["method"] = "POST",
            ["url"] = "http://example.com/submit",
            ["bodyType"] = "form",
            ["formEntries"] = new JsonArray
            {
                new JsonObject { ["name"] = "k1", ["value"] = "a" },
                new JsonObject { ["name"] = "k2", ["value"] = "{start.word}" },
            },
        }, CreateScope());

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.Equal("k1=a&k2=hello", handler.LastBody);
        Assert.Equal("application/x-www-form-urlencoded", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ExecuteAsync_BasicAndApiKeyAuth_SetHeaders()
    {
        // basic：user:pass 的 Base64
        var basicHandler = JsonHandler("""{}""");
        var basicExecutor = BuildExecutor(basicHandler);
        var basicContext = BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
            ["auth"] = new JsonObject { ["type"] = "basic", ["username"] = "user", ["password"] = "pass" },
        });
        await basicExecutor.ExecuteAsync(basicContext, CancellationToken.None);
        Assert.Equal("Basic dXNlcjpwYXNz", basicHandler.LastRequest!.Headers.Authorization?.ToString());

        // apiKey：自定义请求头
        var apiKeyHandler = JsonHandler("""{}""");
        var apiKeyExecutor = BuildExecutor(apiKeyHandler);
        var apiKeyContext = BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
            ["auth"] = new JsonObject { ["type"] = "apiKey", ["headerName"] = "X-Token", ["headerValue"] = "secret" },
        });
        await apiKeyExecutor.ExecuteAsync(apiKeyContext, CancellationToken.None);
        Assert.Equal("secret", apiKeyHandler.LastRequest!.Headers.TryGetValues("X-Token", out var values) ? values.First() : null);
    }

    [Fact]
    public async Task ExecuteAsync_Non2xx_WithoutCapture_Fails()
    {
        var handler = JsonHandler("""{"error":"boom"}""", HttpStatusCode.InternalServerError);
        var executor = BuildExecutor(handler);
        var context = BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
        });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("500", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_Non2xx_WithCapture_CompletesWithHasError()
    {
        var handler = JsonHandler("""{"error":"boom"}""", HttpStatusCode.InternalServerError);
        var executor = BuildExecutor(handler);
        var context = BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
            ["errorCapture"] = true,
            ["extract"] = new JsonArray
            {
                new JsonObject { ["name"] = "title", ["path"] = "$.data.title" },
            },
        });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.True((bool)result.Output["hasError"]!);
        Assert.Equal(500, (int)result.Output["statusCode"]!);
        Assert.Contains("boom", (string?)result.Output["errorMessage"]);
        // JSON 错误体仍可解析为 rawResponse，提取字段无匹配时为 null
        Assert.NotNull(result.Output["rawResponse"]);
        Assert.Null(result.Output["title"]);
    }

    [Fact]
    public async Task ExecuteAsync_TransportError_FailsWithoutCapture_CompletesWithCapture()
    {
        var failing = new StubHttpHandler((_, _) => throw new HttpRequestException("连接被拒绝"));

        var failResult = await BuildExecutor(failing).ExecuteAsync(BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
        }), CancellationToken.None);
        Assert.Equal(NodeState.Failed, failResult.State);
        Assert.Contains("连接被拒绝", failResult.ErrorMessage);

        var captured = await BuildExecutor(failing).ExecuteAsync(BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
            ["errorCapture"] = true,
        }), CancellationToken.None);
        Assert.Equal(NodeState.Completed, captured.State);
        Assert.True((bool)captured.Output["hasError"]!);
        Assert.Null(captured.Output["statusCode"]);
        Assert.Null(captured.Output["rawResponse"]);
    }

    [Fact]
    public async Task ExecuteAsync_TimesOut_FailsWithTimeoutMessage()
    {
        var slow = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(3000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });        var executor = BuildExecutor(slow);
        var context = BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
            ["timeoutSeconds"] = 1,
        });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Failed, result.State);
        Assert.Contains("超时", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUrlOrBadMethod_Fails()
    {
        var executor = BuildExecutor(JsonHandler("{}"));

        var noUrl = await executor.ExecuteAsync(BuildContext(new JsonObject()), CancellationToken.None);
        Assert.Equal(NodeState.Failed, noUrl.State);
        Assert.Contains("未配置请求地址", noUrl.ErrorMessage);

        var badMethod = await executor.ExecuteAsync(BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/",
            ["method"] = "FETCH",
        }), CancellationToken.None);
        Assert.Equal(NodeState.Failed, badMethod.State);
        Assert.Contains("不支持的方法", badMethod.ErrorMessage);

        var badUrl = await executor.ExecuteAsync(BuildContext(new JsonObject
        {
            ["url"] = "ftp://example.com/file",
        }), CancellationToken.None);
        Assert.Equal(NodeState.Failed, badUrl.State);
        Assert.Contains("无效", badUrl.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UrlInterpolation_MissingRefFails_UpstreamRefResolved()
    {
        var executor = BuildExecutor(JsonHandler("{}"));

        var missing = await executor.ExecuteAsync(BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/{ghost.field}",
        }), CancellationToken.None);
        Assert.Equal(NodeState.Failed, missing.State);
        Assert.Contains("解析失败", missing.ErrorMessage);

        var okHandler = JsonHandler("""{}""");
        var ok = await BuildExecutor(okHandler).ExecuteAsync(BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/{start.word}",
        }, CreateScope()), CancellationToken.None);
        Assert.Equal(NodeState.Completed, ok.State);
        Assert.Equal("http://example.com/hello", okHandler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ExecuteAsync_NonJsonResponse_RawResponseIsText()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("plain text", Encoding.UTF8, "text/plain"),
        }));
        var executor = BuildExecutor(handler);
        var context = BuildContext(new JsonObject
        {
            ["url"] = "http://example.com/text",
        });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(NodeState.Completed, result.State);
        Assert.Equal("plain text", (string?)result.Output["rawResponse"]);
    }

    [Fact]
    public void WorkflowValidator_HttpNode_ValidatesConfig()
    {
        var validDefinition = CreateHttpDefinition("http://example.com/{start.word}");
        Assert.Empty(new WorkflowValidator().GetErrors(validDefinition));

        // 非法方法 + 缺地址 + 超时越界 + Body 类型 + 鉴权类型 + 重复提取名 + 非法 JsonPath + 非上游引用
        var http = validDefinition.Nodes.Single(n => n.Type == NodeTypes.Http);
        http.Config = JsonSerializer.SerializeToElement(new
        {
            method = "FETCH",
            timeoutSeconds = 999,
            bodyType = "xml",
            auth = new { type = "oauth" },
            extract = new object[]
            {
                new { name = "a", path = "$.x" },
                new { name = "a", path = "not a path" },
            },
        });
        var errors = new WorkflowValidator().GetErrors(validDefinition);
        Assert.Contains(errors, e => e.Contains("请求方法无效"));
        Assert.Contains(errors, e => e.Contains("未配置请求地址"));
        Assert.Contains(errors, e => e.Contains("超时时长"));
        Assert.Contains(errors, e => e.Contains("请求体类型无效"));
        Assert.Contains(errors, e => e.Contains("鉴权类型无效"));
        Assert.Contains(errors, e => e.Contains("重复的提取字段名"));
        Assert.Contains(errors, e => e.Contains("JsonPath 无效"));

        // 插值引用非上游节点
        http.Config = JsonSerializer.SerializeToElement(new { url = "http://example.com/{end.answer}" });
        errors = new WorkflowValidator().GetErrors(validDefinition);
        Assert.Contains(errors, e => e.Contains("非上游节点"));
    }

    /// <summary>
    /// start(输出 word) → http → end 的最小合法流程.
    /// </summary>
    private static WorkflowDefinition CreateHttpDefinition(string url)
    {
        return new WorkflowDefinition
        {
            Id = "http-e2e",
            Name = "HTTP 流程",
            Version = 1,
            Status = DefinitionStatus.Published,
            Nodes =
            [
                new NodeDefinition { Key = "start", Name = "开始", Type = NodeTypes.Start },
                new NodeDefinition
                {
                    Key = "http1",
                    Name = "HTTP 请求",
                    Type = NodeTypes.Http,
                    Config = JsonSerializer.SerializeToElement(new { url }),
                },
                new NodeDefinition { Key = "end", Name = "结束", Type = NodeTypes.End },
            ],
            Connections =
            [
                new ConnectionDefinition { Id = "c1", Source = "start", Target = "http1" },
                new ConnectionDefinition { Id = "c2", Source = "http1", Target = "end" },
            ],
        };
    }
}
