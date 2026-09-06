using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;
using MoAI.Gateway.Protocols.Upstream;

namespace MoAI.Gateway.Protocols;

/// <summary>
/// 上游分发器：按渠道协议族构建上游请求并转换为中间表示（流式/非流式）.
/// </summary>
public class UpstreamDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly UpstreamProtocolRegistry _registry;
    private readonly ILogger<UpstreamDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpstreamDispatcher"/> class.
    /// </summary>
    /// <param name="httpClient">上游 http 客户端.</param>
    /// <param name="registry">上游协议注册表.</param>
    /// <param name="logger">日志.</param>
    public UpstreamDispatcher(HttpClient httpClient, UpstreamProtocolRegistry registry, ILogger<UpstreamDispatcher> logger)
    {
        _httpClient = httpClient;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// 非流式调用.
    /// </summary>
    /// <param name="channel">渠道.</param>
    /// <param name="model">模型.</param>
    /// <param name="request">归一化请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回归一化响应.</returns>
    public async Task<GatewayChatResponse> SendAsync(AiChannelEntity channel, AiModelEntity model, GatewayChatRequest request, CancellationToken cancellationToken = default)
    {
        var protocol = _registry.Get((AIProtocolFamily)channel.ProtocolFamily);
        using var message = protocol.BuildRequest(channel.BaseUrl, channel.ApiKey, model, request);
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("上游返回错误. Channel={Channel}, Status={Status}, Body={Body}", channel.ProviderKey, (int)response.StatusCode, Truncate(body));
            throw GatewayProtocolException.Upstream((int)response.StatusCode, body);
        }

        return protocol.ParseResponse(body);
    }

    /// <summary>
    /// 流式调用，产出中间表示流事件.
    /// </summary>
    /// <param name="channel">渠道.</param>
    /// <param name="model">模型.</param>
    /// <param name="request">归一化请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回流事件序列.</returns>
    public async IAsyncEnumerable<GatewayStreamEvent> StreamAsync(AiChannelEntity channel, AiModelEntity model, GatewayChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var protocol = _registry.Get((AIProtocolFamily)channel.ProtocolFamily);
        using var message = protocol.BuildRequest(channel.BaseUrl, channel.ApiKey, model, request);
        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("上游流式返回错误. Channel={Channel}, Status={Status}, Body={Body}", channel.ProviderKey, (int)response.StatusCode, Truncate(body));
            throw GatewayProtocolException.Upstream((int)response.StatusCode, body);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == null || !contentType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
        {
            // 部分渠道/代理不支持 SSE，退化为整段响应合成流事件.
            var fullBody = await response.Content.ReadAsStringAsync(cancellationToken);
            foreach (var evt in SynthesizeEvents(protocol, fullBody))
            {
                yield return evt;
            }

            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parser = protocol.CreateSseParser(model.ModelId);
        await foreach (var frame in SseFrameReader.ReadAsync(stream, cancellationToken))
        {
            foreach (var evt in parser.Feed(frame))
            {
                yield return evt;
            }
        }
    }

    private static IEnumerable<GatewayStreamEvent> SynthesizeEvents(IUpstreamProtocol protocol, string body)
    {
        var parsed = protocol.ParseResponse(body);
        yield return new GatewayStreamEvent.Started(parsed.Id, parsed.Model);
        if (!string.IsNullOrEmpty(parsed.Text))
        {
            yield return new GatewayStreamEvent.TextDelta(parsed.Text);
        }

        for (var i = 0; i < parsed.ToolCalls.Count; i++)
        {
            var toolCall = parsed.ToolCalls[i];
            yield return new GatewayStreamEvent.ToolCallStarted(i, toolCall.Id, toolCall.Name);
            yield return new GatewayStreamEvent.ToolCallArgumentsDelta(i, toolCall.ArgumentsJson);
        }

        yield return new GatewayStreamEvent.Finished(parsed.FinishReason, parsed.Usage);
    }

    private static string Truncate(string body)
    {
        return body.Length <= 500 ? body : body[..500];
    }
}
