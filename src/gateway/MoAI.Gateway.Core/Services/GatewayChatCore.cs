using Microsoft.Extensions.Logging;
using MoAI.Gateway.Protocols;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MoAI.Database.Entities;
using MoAI.Gateway.Protocols.Inbound;
using MoAI.Gateway.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.Gateway.Services;

/// <summary>
/// 网关对话编排：解析入口请求 → 解析模型 → 额度预检 → 调用上游（协议转换）→ 记账 → 返回入口格式响应.
/// </summary>
public class GatewayChatCore
{
    private readonly GatewayModelResolver _gatewayModelResolver;
    private readonly GatewayUsageService _gatewayUsageService;
    private readonly UpstreamDispatcher _upstreamDispatcher;
    private readonly InboundProtocolRegistry _inboundRegistry;
    private readonly ILogger<GatewayChatCore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayChatCore"/> class.
    /// </summary>
    public GatewayChatCore(
        GatewayModelResolver gatewayModelResolver,
        GatewayUsageService gatewayUsageService,
        UpstreamDispatcher upstreamDispatcher,
        InboundProtocolRegistry inboundRegistry,
        ILogger<GatewayChatCore> logger)
    {
        _gatewayModelResolver = gatewayModelResolver;
        _gatewayUsageService = gatewayUsageService;
        _upstreamDispatcher = upstreamDispatcher;
        _inboundRegistry = inboundRegistry;
        _logger = logger;
    }

    /// <summary>
    /// 处理一次网关对话请求，直接写入 http 响应.
    /// </summary>
    /// <param name="http">http 上下文.</param>
    /// <param name="format">入口协议格式.</param>
    /// <returns>返回任务.</returns>
    public async Task HandleAsync(HttpContext http, GatewayInboundFormat format)
    {
        var cancellationToken = http.RequestAborted;
        var inbound = _inboundRegistry.Get(format);

        var (teamId, userId, apiKeyId) = ReadContext(http.User);

        JsonElement body;
        try
        {
            using var doc = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: cancellationToken);
            body = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            await WriteErrorAsync(http, inbound, 400, "Request body is not valid JSON.", "invalid_request_error");
            return;
        }

        GatewayChatRequest request;
        try
        {
            request = inbound.ParseRequest(body);
        }
        catch (GatewayProtocolException ex)
        {
            await WriteErrorAsync(http, inbound, ex.StatusCode, ex.Message, ex.Code);
            return;
        }

        (AiModelEntity Model, AiChannelEntity Channel)? resolved;
        try
        {
            resolved = await _gatewayModelResolver.ResolveAsync(teamId, request.Model, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "网关模型解析失败. TeamId={TeamId}, Model={Model}", teamId, request.Model);
            await WriteErrorAsync(http, inbound, 500, "Internal gateway error.", "api_error");
            return;
        }

        if (resolved == null)
        {
            await WriteErrorAsync(http, inbound, 404, $"The model '{request.Model}' does not exist or is not authorized for your team.", "model_not_found");
            return;
        }

        var (model, channel) = resolved.Value;
        if (!string.Equals(model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            await WriteErrorAsync(http, inbound, 400, $"Model '{request.Model}' is not a conversation model.", "invalid_request_error");
            return;
        }

        try
        {
            await _gatewayUsageService.CheckQuotaAsync(model.Id, teamId, cancellationToken);
        }
        catch (BusinessException ex)
        {
            await WriteErrorAsync(http, inbound, ex.StatusCode, ex.Message, "quota_exceeded");
            return;
        }

        try
        {
            if (request.Stream)
            {
                await HandleStreamAsync(http, inbound, request, model, channel, teamId, userId, apiKeyId, cancellationToken);
            }
            else
            {
                await HandleNonStreamAsync(http, inbound, request, model, channel, teamId, userId, apiKeyId, cancellationToken);
            }
        }
        catch (GatewayProtocolException ex)
        {
            await WriteErrorAsync(http, inbound, ex.StatusCode, ex.Message, ex.Code);
        }
        catch (OperationCanceledException)
        {
            // 客户端中断（例如停止生成），属正常现象.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "网关调用失败. TeamId={TeamId}, Model={Model}, Channel={Channel}", teamId, request.Model, channel.ProviderKey);
            await WriteErrorAsync(http, inbound, 502, "Upstream request failed.", "api_error");
        }
    }

    private async Task HandleNonStreamAsync(HttpContext http, IInboundProtocol inbound, GatewayChatRequest request, AiModelEntity model, AiChannelEntity channel, int teamId, long userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var response = await _upstreamDispatcher.SendAsync(channel, model, request, cancellationToken);

        var usage = response.Usage ?? GatewayUsageEstimator.Estimate(request, response.Text);
        await _gatewayUsageService.RecordAsync(model.Id, teamId, userId, apiKeyId, channel.ProviderKey, usage.PromptTokens, usage.CompletionTokens, CancellationToken.None);

        response.Id = inbound.IdPrefix + response.Id;
        http.Response.StatusCode = 200;
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(inbound.BuildResponseJson(response), cancellationToken);
    }

    private async Task HandleStreamAsync(HttpContext http, IInboundProtocol inbound, GatewayChatRequest request, AiModelEntity model, AiChannelEntity channel, int teamId, long userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var responseId = inbound.IdPrefix + GatewayIds.New();
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var renderer = inbound.CreateRenderer(responseId, request.Model, createdAt);

        http.Response.StatusCode = 200;
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        GatewayUsage? usage = null;
        var finishReason = GatewayFinishReason.Stop;
        var text = new System.Text.StringBuilder();

        var events = _upstreamDispatcher.StreamAsync(channel, model, request, cancellationToken);
        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            switch (evt)
            {
                case GatewayStreamEvent.TextDelta textDelta:
                    text.Append(textDelta.Text);
                    break;
                case GatewayStreamEvent.Finished finished:
                    finishReason = finished.Reason;
                    usage = finished.Usage;
                    break;
            }

            foreach (var payload in renderer.Render(evt))
            {
                await WriteFrameAsync(http, payload, cancellationToken);
            }
        }

        usage ??= GatewayUsageEstimator.Estimate(request, text.ToString());
        await _gatewayUsageService.RecordAsync(model.Id, teamId, userId, apiKeyId, channel.ProviderKey, usage.PromptTokens, usage.CompletionTokens, CancellationToken.None);

        foreach (var payload in renderer.RenderDone())
        {
            await WriteFrameAsync(http, payload, cancellationToken);
        }

        await http.Response.Body.FlushAsync(CancellationToken.None);
    }

    private static async Task WriteFrameAsync(HttpContext http, string payload, CancellationToken cancellationToken)
    {
        await http.Response.WriteAsync("data: ", cancellationToken);
        await http.Response.WriteAsync(payload, cancellationToken);
        await http.Response.WriteAsync("\n\n", cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
    }

    private static async Task WriteErrorAsync(HttpContext http, IInboundProtocol inbound, int statusCode, string message, string? code)
    {
        if (http.Response.HasStarted)
        {
            // 流已开始，只能以 SSE 错误帧收尾.
            var frame = new JsonObjectError(inbound.BuildErrorJson(statusCode, message, code));
            http.Response.WriteAsync("data: " + frame.Json + "\n\n").GetAwaiter().GetResult();
            return;
        }

        http.Response.StatusCode = statusCode;
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(inbound.BuildErrorJson(statusCode, message, code));
    }

    private static (int TeamId, long UserId, Guid ApiKeyId) ReadContext(System.Security.Claims.ClaimsPrincipal user)
    {
        var teamId = int.TryParse(user.FindFirst("teamid")?.Value, out var t) ? t : 0;
        var userId = long.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u) ? u : 0;
        var apiKeyId = Guid.TryParse(user.FindFirst("keyid")?.Value, out var k) ? k : Guid.Empty;
        return (teamId, userId, apiKeyId);
    }

    private sealed record JsonObjectError(string Json);
}
