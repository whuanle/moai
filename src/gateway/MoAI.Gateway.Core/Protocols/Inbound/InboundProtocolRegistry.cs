namespace MoAI.Gateway.Protocols.Inbound;

/// <summary>
/// 入口协议注册表.
/// </summary>
public class InboundProtocolRegistry
{
    private readonly IReadOnlyDictionary<GatewayInboundFormat, IInboundProtocol> _protocols;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundProtocolRegistry"/> class.
    /// </summary>
    public InboundProtocolRegistry()
    {
        IInboundProtocol[] all = { new OpenAIChatInbound(), new OpenAIResponsesInbound(), new AnthropicMessagesInbound() };
        _protocols = all.ToDictionary(x => x.Format);
    }

    /// <summary>
    /// 获取入口协议适配器.
    /// </summary>
    /// <param name="format">协议格式.</param>
    /// <returns>返回适配器.</returns>
    public IInboundProtocol Get(GatewayInboundFormat format)
    {
        return _protocols[format];
    }
}
