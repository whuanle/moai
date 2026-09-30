using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 企业微信群机器人推送文本插件配置（每个实例独立保存）.
/// </summary>
public class WeixinWorkWebhookTextConfig
{
    /// <summary>
    /// 机器人 Webhook（完整地址或裸 key）.
    /// </summary>
    [Description("企业微信群机器人 Webhook：可粘贴完整地址（含 key=），也可只填 key 值")]
    public string WebhookKey { get; set; } = string.Empty;
}

/// <summary>
/// 企业微信群机器人推送文本插件请求参数.
/// </summary>
public class WeixinWorkWebhookTextRequest
{
    /// <summary>
    /// 文本内容.
    /// </summary>
    [Description("要推送的文本内容（服务端上限 2048 字节，超出会被截断）")]
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
/// 企业微信群机器人推送文本插件响应.
/// </summary>
public class WeixinWorkWebhookTextResponse
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
