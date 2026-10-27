namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信回调信封（推送报文原文）的最小解析视图.
/// </summary>
public sealed class WeixinWorkCallbackEnvelope
{
    /// <summary>
    /// 企业 ID（信封 ToUserName）.
    /// </summary>
    public string ToUserName { get; init; } = string.Empty;

    /// <summary>
    /// 加密的消息体.
    /// </summary>
    public string Encrypt { get; init; } = string.Empty;

    /// <summary>
    /// 从加密信封 XML 提取 Encrypt 节点，解析失败返回 null.
    /// </summary>
    /// <param name="bodyXml">推送报文原文.</param>
    /// <returns>返回信封视图.</returns>
    public static WeixinWorkCallbackEnvelope? Parse(string bodyXml)
    {
        if (string.IsNullOrWhiteSpace(bodyXml))
        {
            return null;
        }

        try
        {
            var document = System.Xml.Linq.XDocument.Parse(bodyXml);
            var root = document.Root;
            if (root == null || root.Name.LocalName != "xml")
            {
                return null;
            }

            return new WeixinWorkCallbackEnvelope
            {
                ToUserName = root.Element("ToUserName")?.Value ?? string.Empty,
                Encrypt = root.Element("Encrypt")?.Value ?? string.Empty,
            };
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}

/// <summary>
/// 企业微信推送明文报文（解密后 XML）的最小解析视图.
/// </summary>
public sealed class WeixinWorkCallbackMessage
{
    /// <summary>
    /// 企业 ID（ToUserName）.
    /// </summary>
    public string ToUserName { get; init; } = string.Empty;

    /// <summary>
    /// 发送消息的成员 userid.
    /// </summary>
    public string FromUserName { get; init; } = string.Empty;

    /// <summary>
    /// 消息类型：text/event 等.
    /// </summary>
    public string MsgType { get; init; } = string.Empty;

    /// <summary>
    /// 文本消息内容，非 text 类型为空字符串.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 消息 id（MsgId），去重用；部分事件推送无该字段.
    /// </summary>
    public string MsgId { get; init; } = string.Empty;

    /// <summary>
    /// 群聊 id，群聊消息独有；单聊为空字符串.
    /// </summary>
    public string ChatId { get; init; } = string.Empty;

    /// <summary>
    /// 接收消息的应用 AgentID.
    /// </summary>
    public int AgentId { get; init; }

    /// <summary>
    /// 消息发送时间（秒级时间戳）.
    /// </summary>
    public long CreateTime { get; init; }

    /// <summary>
    /// 解析明文报文 XML，解析失败返回 null.
    /// </summary>
    /// <param name="messageXml">解密后的报文 XML.</param>
    /// <returns>返回报文视图.</returns>
    public static WeixinWorkCallbackMessage? Parse(string messageXml)
    {
        if (string.IsNullOrWhiteSpace(messageXml))
        {
            return null;
        }

        try
        {
            var document = System.Xml.Linq.XDocument.Parse(messageXml);
            var root = document.Root;
            if (root == null || root.Name.LocalName != "xml")
            {
                return null;
            }

            string GetText(string name) => root.Element(name)?.Value ?? string.Empty;
            int.TryParse(GetText("AgentID"), out var agentId);
            long.TryParse(GetText("CreateTime"), out var createTime);

            return new WeixinWorkCallbackMessage
            {
                ToUserName = GetText("ToUserName"),
                FromUserName = GetText("FromUserName"),
                MsgType = GetText("MsgType"),
                Content = GetText("Content"),
                MsgId = GetText("MsgId"),
                ChatId = GetText("ChatId"),
                AgentId = agentId,
                CreateTime = createTime,
            };
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}
