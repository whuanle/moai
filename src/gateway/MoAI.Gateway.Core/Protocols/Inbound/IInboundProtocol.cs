using System.Text.Json;

namespace MoAI.Gateway.Protocols.Inbound;

/// <summary>
/// 团队用户接入网关使用的入口协议格式.
/// </summary>
public enum GatewayInboundFormat
{
    /// <summary>
    /// OpenAI Chat Completions（/v1/chat/completions）.
    /// </summary>
    OpenAIChatCompletions,

    /// <summary>
    /// OpenAI Responses（/v1/responses）.
    /// </summary>
    OpenAIResponses,

    /// <summary>
    /// Anthropic Messages（/v1/messages）.
    /// </summary>
    AnthropicMessages,
}

/// <summary>
/// 入口协议适配器：入口 wire 格式 ⇄ 网关中间表示.
/// </summary>
public interface IInboundProtocol
{
    /// <summary>
    /// 协议格式.
    /// </summary>
    GatewayInboundFormat Format { get; }

    /// <summary>
    /// 响应 id 前缀，如 chatcmpl-.
    /// </summary>
    string IdPrefix { get; }

    /// <summary>
    /// 解析入口请求为归一化请求.
    /// </summary>
    /// <param name="body">请求体.</param>
    /// <returns>返回归一化请求.</returns>
    GatewayChatRequest ParseRequest(JsonElement body);

    /// <summary>
    /// 组装非流式响应 JSON.
    /// </summary>
    /// <param name="response">归一化响应.</param>
    /// <returns>返回 JSON 字符串.</returns>
    string BuildResponseJson(GatewayChatResponse response);

    /// <summary>
    /// 组装错误响应 JSON（协议自身的错误信封）.
    /// </summary>
    /// <param name="statusCode">http 状态码.</param>
    /// <param name="message">错误信息.</param>
    /// <param name="code">错误码.</param>
    /// <returns>返回 JSON 字符串.</returns>
    string BuildErrorJson(int statusCode, string message, string? code);

    /// <summary>
    /// 创建流式渲染器，把中间表示流事件渲染为入口协议的 SSE data 载荷.
    /// </summary>
    /// <param name="responseId">响应 id.</param>
    /// <param name="model">模型名.</param>
    /// <param name="createdAt">unix 创建时间.</param>
    /// <returns>返回渲染器.</returns>
    IInboundStreamRenderer CreateRenderer(string responseId, string model, long createdAt);
}

/// <summary>
/// 流式渲染器.
/// </summary>
public interface IInboundStreamRenderer
{
    /// <summary>
    /// 渲染一个流事件为 0..n 个 SSE data 载荷（JSON 字符串，不含 "data:" 前缀）.
    /// </summary>
    /// <param name="evt">流事件.</param>
    /// <returns>返回载荷集合.</returns>
    IEnumerable<string> Render(GatewayStreamEvent evt);

    /// <summary>
    /// 渲染结束帧，如 OpenAI 的 [DONE]；无结束帧的协议返回空集合.
    /// </summary>
    /// <returns>返回载荷集合.</returns>
    IEnumerable<string> RenderDone();
}
