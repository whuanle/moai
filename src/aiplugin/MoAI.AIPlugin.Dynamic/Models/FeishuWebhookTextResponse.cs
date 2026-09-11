using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 飞书机器人发送文本消息响应结果.
/// </summary>
public class FeishuWebhookTextResponse
{
    /// <summary>
    /// 飞书返回的业务状态码，0 表示推送成功.
    /// </summary>
    [Description("飞书返回的业务状态码，0 表示推送成功；非 0 时插件会直接以失败结果返回，不会走到这里")]
    public int Code { get; set; }

    /// <summary>
    /// 飞书返回的说明信息.
    /// </summary>
    [Description("飞书返回的说明信息，成功时为 success")]
    public string Msg { get; set; } = string.Empty;

    /// <summary>
    /// 本次实际推送的文本内容.
    /// </summary>
    [Description("本次实际推送的文本内容")]
    public string Text { get; set; } = string.Empty;
}
