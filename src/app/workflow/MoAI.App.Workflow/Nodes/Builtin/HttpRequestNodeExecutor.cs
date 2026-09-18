using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Json.Path;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// HTTP 键值对（查询参数/请求头/表单字段），值支持 {引用} 插值（与引擎 Interpolation 表达式一致）.
/// </summary>
public class HttpKeyValue
{
    /// <summary>
    /// 名称（参数名/请求头名/表单字段名）.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 值（支持 {引用} 插值）.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// HTTP 请求节点鉴权配置.
/// </summary>
public class HttpAuthConfig
{
    /// <summary>
    /// 鉴权类型：none / bearer / basic / apiKey.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "none";

    /// <summary>
    /// Bearer Token（type=bearer）.
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>
    /// 用户名（type=basic）.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// 密码（type=basic）.
    /// </summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>
    /// 自定义请求头名（type=apiKey，默认 X-API-Key）.
    /// </summary>
    [JsonPropertyName("headerName")]
    public string? HeaderName { get; set; }

    /// <summary>
    /// 自定义请求头值（type=apiKey）.
    /// </summary>
    [JsonPropertyName("headerValue")]
    public string? HeaderValue { get; set; }
}

/// <summary>
/// HTTP 请求节点输出字段提取配置：对响应 JSON 执行 JsonPath 提取为输出变量.
/// </summary>
public class HttpExtractField
{
    /// <summary>
    /// 输出变量名.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// JsonPath 表达式（如 $.data.title）.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型（设计器元数据）.
    /// </summary>
    [JsonPropertyName("fieldType")]
    public string? FieldType { get; set; }
}

/// <summary>
/// HTTP 请求节点配置（config JSON）.
/// </summary>
public class HttpRequestNodeConfig
{
    /// <summary>
    /// 请求方法：GET/POST/PUT/DELETE/PATCH/HEAD，默认 GET.
    /// </summary>
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    /// <summary>
    /// 请求地址（支持 {引用} 插值）.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 超时时长（秒，1-300，默认 30）.
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 查询参数.
    /// </summary>
    [JsonPropertyName("params")]
    public List<HttpKeyValue>? Params { get; set; }

    /// <summary>
    /// 请求头.
    /// </summary>
    [JsonPropertyName("headers")]
    public List<HttpKeyValue>? Headers { get; set; }

    /// <summary>
    /// 请求体类型：none / json / form / text；缺省时有 body 视为 json.
    /// </summary>
    [JsonPropertyName("bodyType")]
    public string? BodyType { get; set; }

    /// <summary>
    /// 请求体（json/text，支持 {引用} 插值）.
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>
    /// 表单字段（bodyType=form）.
    /// </summary>
    [JsonPropertyName("formEntries")]
    public List<HttpKeyValue>? FormEntries { get; set; }

    /// <summary>
    /// 鉴权配置.
    /// </summary>
    [JsonPropertyName("auth")]
    public HttpAuthConfig? Auth { get; set; }

    /// <summary>
    /// 报错捕获：开启后请求失败/非 2xx 不再中断流程，输出 hasError/errorMessage.
    /// </summary>
    [JsonPropertyName("errorCapture")]
    public bool? ErrorCapture { get; set; }

    /// <summary>
    /// 输出字段提取.
    /// </summary>
    [JsonPropertyName("extract")]
    public List<HttpExtractField>? Extract { get; set; }
}

/// <summary>
/// HTTP 请求节点执行器 - 发起自定义 HTTP 请求并提取响应字段.
/// 配置：method/url/timeoutSeconds/params/headers/bodyType/body/formEntries/auth/errorCapture/extract；
/// URL、参数值、请求头值、请求体均支持 {引用} 插值（sys.*、system.*、input.*、上游节点输出），
/// 其中 URL 的引用必须可解析（解析失败节点失败），其余位置解析失败置空串.
/// 输出：{ statusCode, rawResponse, hasError, errorMessage, ...extract 字段 }；
/// rawResponse 为响应体（JSON 可解析时为对象/数组，否则为文本）.
/// 未开启报错捕获时，网络错误/超时/非 2xx 状态码使节点失败（实例挂起）；开启后节点完成并输出 hasError=true.
/// </summary>
public partial class HttpRequestNodeExecutor : INodeExecutor
{
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxTimeoutSeconds = 300;

    /// <summary>
    /// 响应体读取上限（字节），防止超大响应拖垮内存.
    /// </summary>
    internal const long MaxResponseBytes = 5 * 1024 * 1024;

    private static readonly HttpClient SharedClient = CreateSharedClient();

    /// <summary>
    /// 允许的请求方法.
    /// </summary>
    public static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD"];

    /// <summary>
    /// 允许的请求体类型.
    /// </summary>
    public static readonly string[] AllowedBodyTypes = ["none", "json", "form", "text"];

    /// <summary>
    /// 允许的鉴权类型.
    /// </summary>
    public static readonly string[] AllowedAuthTypes = ["none", "bearer", "basic", "apiKey"];

    private static readonly JsonSerializerOptions ConfigOptions = new() { PropertyNameCaseInsensitive = true };

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex InterpolationRegex();

    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly HttpMessageHandler? _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestNodeExecutor"/> class.
    /// </summary>
    public HttpRequestNodeExecutor(IExpressionEvaluator expressionEvaluator, HttpMessageHandler? handler = null)
    {
        _expressionEvaluator = expressionEvaluator;
        _handler = handler;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.Http;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        HttpRequestNodeConfig config;
        try
        {
            config = context.Config.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<HttpRequestNodeConfig>(context.Config.GetRawText(), ConfigOptions) ?? new HttpRequestNodeConfig()
                : new HttpRequestNodeConfig();
        }
        catch (JsonException ex)
        {
            return NodeExecutionResult.Failure($"HTTP 请求节点配置格式无效：{ex.Message}");
        }

        // 1. 方法与地址
        var method = string.IsNullOrWhiteSpace(config.Method) ? "GET" : config.Method.Trim().ToUpperInvariant();
        if (!AllowedMethods.Contains(method))
        {
            return NodeExecutionResult.Failure($"HTTP 请求节点使用了不支持的方法：{method}（允许：{string.Join("/", AllowedMethods)}）");
        }

        if (string.IsNullOrWhiteSpace(config.Url))
        {
            return NodeExecutionResult.Failure("HTTP 请求节点未配置请求地址（config.url）");
        }

        string urlText;
        try
        {
            urlText = ResolveTemplateStrict(config.Url, context.Scope).Trim();
        }
        catch (WorkflowException ex)
        {
            return NodeExecutionResult.Failure($"HTTP 请求节点请求地址解析失败：{ex.Message}");
        }

        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return NodeExecutionResult.Failure($"HTTP 请求节点请求地址无效（必须是 http/https 绝对地址）：{urlText}");
        }

        var timeoutSeconds = Math.Clamp(config.TimeoutSeconds ?? DefaultTimeoutSeconds, 1, MaxTimeoutSeconds);
        var useSharedClient = _handler == null;

        try
        {
            // 2. 组装请求
            using var request = new HttpRequestMessage(new HttpMethod(method), BuildRequestUri(uri, config.Params, context.Scope));

            foreach (var header in config.Headers ?? [])
            {
                if (string.IsNullOrWhiteSpace(header.Name))
                {
                    continue;
                }

                request.Headers.TryAddWithoutValidation(header.Name.Trim(), ResolveQuiet(header.Value, context.Scope));
            }

            ApplyAuth(request, config.Auth, context.Scope);

            var bodyType = ResolveBodyType(config);
            if (method != HttpMethod.Head.Method && bodyType != "none")
            {
                request.Content = BuildContent(bodyType, config, context.Scope);
            }

            // 3. 发送并读取响应（超时用独立 CTS 控制；用户取消时原样抛出由调度器处理）
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var client = useSharedClient ? SharedClient : new HttpClient(_handler!);
            var statusCode = 0;
            var bodyText = string.Empty;
            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
                statusCode = (int)response.StatusCode;
                bodyText = await ReadBodyAsync(response.Content, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return FailOrCapture(config, $"请求超时（{timeoutSeconds} 秒）：{urlText}", null, null);
            }
            catch (HttpRequestException ex)
            {
                return FailOrCapture(config, $"请求失败：{ex.Message}（{urlText}）", null, null);
            }

            // 4. 解析响应体（JSON 可解析为对象/数组，否则保留原文）
            JsonNode? rawResponse = null;
            if (!string.IsNullOrEmpty(bodyText))
            {
                try
                {
                    rawResponse = JsonNode.Parse(bodyText);
                }
                catch (JsonException)
                {
                    rawResponse = null;
                }
            }

            var rawOutput = rawResponse ?? (bodyText.Length > 0 ? JsonValue.Create(bodyText) : null);

            if (statusCode >= 400)
            {
                return FailOrCapture(config, $"上游返回错误状态码 {statusCode}：{Truncate(bodyText, 200)}", statusCode, rawOutput);
            }

            // 5. 提取输出字段
            var output = BuildOutput(statusCode, rawOutput, hasError: false, errorMessage: null, config.Extract);
            return NodeExecutionResult.Success(output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"HTTP 请求执行失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 定义校验（发布/调试执行前由 <see cref="WorkflowValidator"/> 调用）：方法/地址/超时/Body 类型/鉴权类型/提取字段与插值引用.
    /// </summary>
    /// <param name="node">HTTP 节点定义.</param>
    /// <param name="nodeKeys">全部节点 Key 集合.</param>
    /// <param name="ancestors">该节点的全部祖先节点 Key（插值引用必须是上游节点）.</param>
    /// <returns>错误列表，空表示合法.</returns>
    public static IReadOnlyList<string> ValidateDefinition(NodeDefinition node, IEnumerable<string> nodeKeys, HashSet<string> ancestors)
    {
        var errors = new List<string>();
        HttpRequestNodeConfig config;
        try
        {
            config = node.Config.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<HttpRequestNodeConfig>(node.Config.GetRawText(), ConfigOptions) ?? new HttpRequestNodeConfig()
                : new HttpRequestNodeConfig();
        }
        catch (JsonException ex)
        {
            return [$"HTTP 请求节点 {node.Key} 配置格式无效：{ex.Message}"];
        }

        if (!string.IsNullOrWhiteSpace(config.Method) && !AllowedMethods.Contains(config.Method.Trim().ToUpperInvariant()))
        {
            errors.Add($"HTTP 请求节点 {node.Key} 的请求方法无效：{config.Method}（允许：{string.Join("/", AllowedMethods)}）");
        }

        if (string.IsNullOrWhiteSpace(config.Url))
        {
            errors.Add($"HTTP 请求节点 {node.Key} 未配置请求地址");
        }

        if (config.TimeoutSeconds.HasValue && (config.TimeoutSeconds < 1 || config.TimeoutSeconds > MaxTimeoutSeconds))
        {
            errors.Add($"HTTP 请求节点 {node.Key} 的超时时长必须在 1-{MaxTimeoutSeconds} 秒之间，当前为 {config.TimeoutSeconds}");
        }

        if (!string.IsNullOrWhiteSpace(config.BodyType) && !AllowedBodyTypes.Contains(config.BodyType))
        {
            errors.Add($"HTTP 请求节点 {node.Key} 的请求体类型无效：{config.BodyType}（允许：{string.Join("/", AllowedBodyTypes)}）");
        }

        if (config.Auth != null && !AllowedAuthTypes.Contains(config.Auth.Type ?? "none"))
        {
            errors.Add($"HTTP 请求节点 {node.Key} 的鉴权类型无效：{config.Auth.Type}（允许：{string.Join("/", AllowedAuthTypes)}）");
        }

        // 提取字段：名称非空且不重复、JsonPath 必填且可解析
        var keySet = new HashSet<string>(nodeKeys, StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in config.Extract ?? [])
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                errors.Add($"HTTP 请求节点 {node.Key} 存在名称为空的提取字段");
                continue;
            }

            if (!seenNames.Add(field.Name.Trim()))
            {
                errors.Add($"HTTP 请求节点 {node.Key} 存在重复的提取字段名：{field.Name}");
            }

            if (string.IsNullOrWhiteSpace(field.Path))
            {
                errors.Add($"HTTP 请求节点 {node.Key} 的提取字段 {field.Name} 缺少 JsonPath 表达式");
                continue;
            }

            try
            {
                JsonPath.Parse(field.Path);
            }
            catch (PathParseException ex)
            {
                errors.Add($"HTTP 请求节点 {node.Key} 的提取字段 {field.Name} JsonPath 无效：{field.Path}，{ex.Message}");
            }
        }

        // 插值引用：url/参数/请求头/请求体/表单中的 {引用} 必须指向 sys/system/input 或上游节点
        void CheckRefs(string? template, string fieldLabel)
        {
            if (string.IsNullOrEmpty(template))
            {
                return;
            }

            foreach (Match match in InterpolationRegex().Matches(template))
            {
                var reference = match.Groups[1].Value.Trim();
                var prefix = reference.Split('.').FirstOrDefault();
                if (string.IsNullOrEmpty(prefix) || prefix == "sys" || prefix == "system" || prefix == "input")
                {
                    continue;
                }

                if (!keySet.Contains(prefix))
                {
                    errors.Add($"HTTP 请求节点 {node.Key} 的{fieldLabel}引用了不存在的节点：{{{reference}}}");
                }
                else if (!ancestors.Contains(prefix))
                {
                    errors.Add($"HTTP 请求节点 {node.Key} 的{fieldLabel}引用了非上游节点：{{{reference}}}（只有上游节点的输出可以被引用）");
                }
            }
        }

        CheckRefs(config.Url, "请求地址");
        foreach (var item in config.Params ?? [])
        {
            CheckRefs(item.Value, $"查询参数 {item.Name}");
        }

        foreach (var item in config.Headers ?? [])
        {
            CheckRefs(item.Value, $"请求头 {item.Name}");
        }

        CheckRefs(config.Body, "请求体");
        foreach (var item in config.FormEntries ?? [])
        {
            CheckRefs(item.Value, $"表单字段 {item.Name}");
        }

        return errors;
    }

    private static HttpClient CreateSharedClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10),
            MaxResponseContentBufferSize = MaxResponseBytes,
        };
    }

    private static string ResolveBodyType(HttpRequestNodeConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.BodyType))
        {
            return config.BodyType;
        }

        return string.IsNullOrEmpty(config.Body) && (config.FormEntries == null || config.FormEntries.Count == 0) ? "none" : "json";
    }

    /// <summary>
    /// 合并 URL 已有查询串与配置的查询参数（参数值经插值解析并 URL 编码）.
    /// </summary>
    private static string BuildRequestUri(Uri uri, List<HttpKeyValue>? parameters, IWorkflowVariableScope scope)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return uri.AbsoluteUri;
        }

        var existingQuery = uri.Query.TrimStart('?');
        var extra = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => $"{Uri.EscapeDataString(p.Name.Trim())}={Uri.EscapeDataString(ResolveQuiet(p.Value, scope))}");
        var merged = string.Join("&", new[] { existingQuery }.Concat(extra).Where(s => !string.IsNullOrEmpty(s)));
        return new UriBuilder(uri) { Query = merged }.Uri.AbsoluteUri;
    }

    private static HttpContent BuildContent(string bodyType, HttpRequestNodeConfig config, IWorkflowVariableScope scope)
    {
        switch (bodyType)
        {
            case "json":
                return new StringContent(ResolveQuiet(config.Body, scope), Encoding.UTF8, "application/json");
            case "text":
                return new StringContent(ResolveQuiet(config.Body, scope), Encoding.UTF8, "text/plain");
            case "form":
                var entries = (config.FormEntries ?? [])
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .ToDictionary(e => e.Name.Trim(), e => ResolveQuiet(e.Value, scope));
                return new FormUrlEncodedContent(entries);
            default:
                throw new WorkflowException($"不支持的请求体类型：{bodyType}");
        }
    }

    private static void ApplyAuth(HttpRequestMessage request, HttpAuthConfig? auth, IWorkflowVariableScope scope)
    {
        if (auth == null || string.IsNullOrWhiteSpace(auth.Type) || auth.Type == "none")
        {
            return;
        }

        switch (auth.Type)
        {
            case "bearer":
                var token = ResolveQuiet(auth.Token, scope);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                break;
            case "basic":
                var username = ResolveQuiet(auth.Username, scope);
                var password = ResolveQuiet(auth.Password, scope);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
                break;
            case "apiKey":
                var headerName = string.IsNullOrWhiteSpace(auth.HeaderName) ? "X-API-Key" : auth.HeaderName.Trim();
                var headerValue = ResolveQuiet(auth.HeaderValue, scope);
                if (!string.IsNullOrEmpty(headerValue))
                {
                    request.Headers.TryAddWithoutValidation(headerName, headerValue);
                }

                break;
        }
    }

    /// <summary>
    /// URL 的插值解析（严格模式）：引用缺失时抛出 WorkflowException，避免请求落到错误地址.
    /// </summary>
    private string ResolveTemplateStrict(string template, IWorkflowVariableScope scope)
    {
        return _expressionEvaluator.Evaluate(
            new FieldBinding { ExpressionType = ExpressionType.Interpolation, Value = template },
            scope) is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
    }

    /// <summary>
    /// 插值安静解析：模板中的 {引用} 替换为变量值（字符串取原文，复杂值序列化为 JSON 文本）；
    /// 变量缺失（如分支未执行）时置空串而非报错.
    /// </summary>
    private static string ResolveQuiet(string? template, IWorkflowVariableScope scope)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return InterpolationRegex().Replace(template, match =>
        {
            var reference = match.Groups[1].Value.Trim();
            if (!scope.TryResolve(reference, out var value) || value == null)
            {
                return string.Empty;
            }

            return value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue)
                ? stringValue
                : value.ToJsonString();
        });
    }

    private static NodeExecutionResult FailOrCapture(HttpRequestNodeConfig config, string errorMessage, int? statusCode, JsonNode? rawResponse)
    {
        if (config.ErrorCapture != true)
        {
            return NodeExecutionResult.Failure(errorMessage);
        }

        return NodeExecutionResult.Success(BuildOutput(statusCode, rawResponse, hasError: true, errorMessage: errorMessage, config.Extract));
    }

    private static JsonObject BuildOutput(int? statusCode, JsonNode? rawResponse, bool hasError, string? errorMessage, List<HttpExtractField>? extract)
    {
        var output = new JsonObject
        {
            ["statusCode"] = statusCode.HasValue ? JsonValue.Create(statusCode.Value) : null,
            ["rawResponse"] = rawResponse?.DeepClone(),
            ["hasError"] = hasError,
            ["errorMessage"] = errorMessage,
        };

        // 提取字段基于响应 JSON 求值；无 JSON（或报错无响应）时全部置 null，保持输出结构稳定
        if (extract != null)
        {
            foreach (var field in extract)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    continue;
                }

                output[field.Name.Trim()] = ExtractValue(rawResponse, field.Path);
            }
        }

        return output;
    }

    private static JsonNode? ExtractValue(JsonNode? response, string path)
    {
        if (response == null)
        {
            return null;
        }

        JsonPath jsonPath;
        try
        {
            jsonPath = JsonPath.Parse(path);
        }
        catch (PathParseException)
        {
            throw new WorkflowException($"提取字段 JsonPath 无效：{path}");
        }

        var results = jsonPath.Evaluate(response);
        if (results.Matches.Count == 0)
        {
            return null;
        }

        if (results.Matches.Count == 1)
        {
            return results.Matches[0].Value?.DeepClone();
        }

        var array = new JsonArray();
        foreach (var match in results.Matches)
        {
            array.Add(match.Value?.DeepClone());
        }

        return array;
    }

    private static async Task<string> ReadBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            return string.Empty;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var buffer = new char[81920];
        var builder = new StringBuilder();
        long total = 0;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            total += read;
            if (total > MaxResponseBytes)
            {
                throw new WorkflowException($"响应体超过 {MaxResponseBytes} 字节上限");
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "…";
    }
}
