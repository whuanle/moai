using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 飞书机器人发送文本消息插件配置（每个实例独立保存）.
/// </summary>
public class FeishuWebhookTextConfig
{
    /// <summary>
    /// 飞书群自定义机器人的 Webhook 地址或其中的 token.
    /// </summary>
    [Description("飞书群自定义机器人 Webhook：可直接粘贴完整地址（https://open.feishu.cn/open-apis/bot/v2/hook/xxx），也可只填最后的 token")]
    public string WebhookKey { get; set; } = string.Empty;

    /// <summary>
    /// 开启签名校验时使用的密钥，未开启签名校验时为<see langword="null"/>或空字符串.
    /// </summary>
    [Description("机器人安全设置中选择「签名校验」时填写这里的密钥；未开启签名校验时留空")]
    public string? SignKey { get; set; }
}
