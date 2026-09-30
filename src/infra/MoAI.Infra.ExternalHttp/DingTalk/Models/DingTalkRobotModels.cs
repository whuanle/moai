using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoAI.Infra.DingTalk.Models;

/// <summary>
/// 钉钉自定义机器人 text 消息请求体.
/// </summary>
public class DingTalkRobotTextRequest
{
    /// <summary>
    /// 消息类型，固定 text.
    /// </summary>
    [JsonPropertyName("msgtype")]
    public string Msgtype { get; set; } = "text";

    /// <summary>
    /// 文本消息体.
    /// </summary>
    [JsonPropertyName("text")]
    public DingTalkRobotTextBody Text { get; set; } = new();

    /// <summary>
    /// @人的信息（可空，序列化时忽略）.
    /// </summary>
    [JsonPropertyName("at")]
    public DingTalkRobotAt? At { get; set; }
}

/// <summary>
/// 钉钉机器人 text 消息体.
/// </summary>
public class DingTalkRobotTextBody
{
    /// <summary>
    /// 文本内容（包含手机号可在客户端渲染 @）.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 钉钉机器人 @ 信息.
/// </summary>
public class DingTalkRobotAt
{
    /// <summary>
    /// 被 @ 人的手机号列表.
    /// </summary>
    [JsonPropertyName("atMobiles")]
    public List<string>? AtMobiles { get; set; }

    /// <summary>
    /// 是否 @ 所有人.
    /// </summary>
    [JsonPropertyName("isAtAll")]
    public bool? IsAtAll { get; set; }
}

/// <summary>
/// 钉钉机器人响应（errcode=0 为成功）.
/// </summary>
public class DingTalkRobotResponse
{
    /// <summary>
    /// 业务状态码，0 为成功.
    /// </summary>
    [JsonPropertyName("errcode")]
    public int Errcode { get; set; }

    /// <summary>
    /// 状态描述.
    /// </summary>
    [JsonPropertyName("errmsg")]
    public string? Errmsg { get; set; }
}
