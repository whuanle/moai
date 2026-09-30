using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 钉钉机器人推送文本插件配置（每个实例独立保存）.
/// </summary>
public class DingTalkWebhookTextConfig
{
    /// <summary>
    /// 机器人 Webhook（完整地址或裸 access_token）.
    /// </summary>
    [Description("钉钉群自定义机器人 Webhook：可粘贴完整地址（含 access_token=），也可只填 access_token 值")]
    public string WebhookKey { get; set; } = string.Empty;

    /// <summary>
    /// 加签密钥（可选）.
    /// </summary>
    [Description("机器人安全设置选择「加签」时填密钥（SEC 开头），未开启留空")]
    public string Secret { get; set; } = string.Empty;
}

/// <summary>
/// 钉钉机器人推送文本插件请求参数.
/// </summary>
public class DingTalkWebhookTextRequest
{
    /// <summary>
    /// 文本内容.
    /// </summary>
    [Description("要推送的文本内容")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 被 @ 人的手机号.
    /// </summary>
    [Description("被 @ 人的手机号（逗号分隔，可空）")]
    public string? AtMobiles { get; set; }

    /// <summary>
    /// 是否 @ 所有人.
    /// </summary>
    [Description("是否 @ 所有人（默认 false）")]
    public bool AtAll { get; set; }
}

/// <summary>
/// 钉钉机器人推送文本插件响应.
/// </summary>
public class DingTalkWebhookTextResponse
{
    /// <summary>
    /// 业务状态码（0 为成功）.
    /// </summary>
    public int Errcode { get; set; }

    /// <summary>
    /// 状态描述.
    /// </summary>
    public string Errmsg { get; set; } = string.Empty;

    /// <summary>
    /// 实际推送的文本.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
