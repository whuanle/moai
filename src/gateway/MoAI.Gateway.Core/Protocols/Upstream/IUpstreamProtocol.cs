using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;

namespace MoAI.Gateway.Protocols.Upstream;

/// <summary>
/// 上游协议适配器：网关中间表示 → 上游渠道 wire 格式.
/// </summary>
public interface IUpstreamProtocol
{
    /// <summary>
    /// 渠道协议族.
    /// </summary>
    AIProtocolFamily Family { get; }

    /// <summary>
    /// 构建上游 http 请求.
    /// </summary>
    /// <param name="baseUrl">渠道接入端点.</param>
    /// <param name="apiKey">渠道密钥.</param>
    /// <param name="model">上游模型.</param>
    /// <param name="request">归一化请求.</param>
    /// <returns>返回 http 请求消息.</returns>
    HttpRequestMessage BuildRequest(string baseUrl, string apiKey, AiModelEntity model, GatewayChatRequest request);

    /// <summary>
    /// 解析上游非流式响应为归一化响应.
    /// </summary>
    /// <param name="body">上游响应体.</param>
    /// <returns>返回归一化响应.</returns>
    GatewayChatResponse ParseResponse(string body);

    /// <summary>
    /// 创建上游流式解析器.
    /// </summary>
    /// <param name="model">上游模型名.</param>
    /// <returns>返回解析器.</returns>
    IUpstreamSseParser CreateSseParser(string model);
}

/// <summary>
/// 上游 SSE 流解析器：逐帧喂数据，产出中间表示流事件.
/// </summary>
public interface IUpstreamSseParser
{
    /// <summary>
    /// 喂入一帧 SSE data 载荷.
    /// </summary>
    /// <param name="data">data 载荷（[DONE] 由实现方处理）.</param>
    /// <returns>返回产生的流事件.</returns>
    IEnumerable<GatewayStreamEvent> Feed(string data);
}

/// <summary>
/// 上游协议注册表.
/// </summary>
public class UpstreamProtocolRegistry
{
    /// <summary>
    /// 协议族 → 适配器.
    /// </summary>
    private readonly IReadOnlyDictionary<AIProtocolFamily, IUpstreamProtocol> _protocols;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpstreamProtocolRegistry"/> class.
    /// </summary>
    public UpstreamProtocolRegistry()
    {
        IUpstreamProtocol[] all = { new OpenAIChatUpstream(), new OpenAIResponsesUpstream(), new AnthropicUpstream(), new GeminiUpstream() };
        _protocols = all.ToDictionary(x => x.Family);
    }

    /// <summary>
    /// 获取上游协议适配器.
    /// </summary>
    /// <param name="family">协议族.</param>
    /// <returns>返回适配器.</returns>
    /// <exception cref="GatewayProtocolException">不支持的协议.</exception>
    public IUpstreamProtocol Get(AIProtocolFamily family)
    {
        if (!_protocols.TryGetValue(family, out var protocol))
        {
            throw new GatewayProtocolException(400, $"不支持的渠道协议类型: {family}.", "unsupported_protocol");
        }

        return protocol;
    }
}

/// <summary>
/// 上游协议公共助手.
/// </summary>
internal static class UpstreamHelper
{
    /// <summary>
    /// 解析 JSON 字符串，失败抛协议异常.
    /// </summary>
    public static JsonElement Parse(string body)
    {
        try
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new GatewayProtocolException(502, $"上游返回了无法解析的响应: {ex.Message}.", "upstream_error");
        }
    }

    /// <summary>
    /// OpenAI 系 base url 归一化：渠道端点未带路径时补 /v1（与渠道同步模型逻辑一致）.
    /// </summary>
    public static string NormalizeOpenAiBase(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        var path = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.AbsolutePath : string.Empty;
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return trimmed + "/v1";
        }

        return trimmed;
    }

    /// <summary>
    /// JSON Schema 元素转 JsonNode，null 时返回空对象.
    /// </summary>
    public static JsonNode SchemaToNode(JsonElement? schema)
    {
        if (schema == null)
        {
            return new JsonObject();
        }

        return JsonNode.Parse(schema.Value.GetRawText()) ?? new JsonObject();
    }

    /// <summary>
    /// 图片片段转为 OpenAI content part.
    /// </summary>
    public static JsonObject? ImageToOpenAiPart(GatewayImagePart part)
    {
        return new JsonObject
        {
            ["type"] = "image_url",
            ["image_url"] = new JsonObject { ["url"] = part.Url },
        };
    }
}
