using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 飞书机器人发送文本消息请求参数.
/// </summary>
public class FeishuWebhookTextRequest
{
    /// <summary>
    /// 要推送的文本内容.
    /// </summary>
    [Description("要推送到飞书群聊的文本内容")]
    public string Text { get; set; } = string.Empty;
}
