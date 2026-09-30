using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoAI.Infra.WeixinWork.Models;

/// <summary>
/// 企业微信群机器人 text 消息请求体.
/// </summary>
public class WeixinWorkRobotTextRequest
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
    public WeixinWorkRobotTextBody Text { get; set; } = new();
}

/// <summary>
/// 企业微信群机器人 text 消息体.
/// </summary>
public class WeixinWorkRobotTextBody
{
    /// <summary>
    /// 文本内容（最长 2048 字节，超出由调用方截断）.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 被 @ 的成员列表，"@all" 表示所有人（可空）.
    /// </summary>
    [JsonPropertyName("mentioned_list")]
    public List<string>? MentionedList { get; set; }

    /// <summary>
    /// 被 @ 的成员手机号列表（可空）.
    /// </summary>
    [JsonPropertyName("mentioned_mobile_list")]
    public List<string>? MentionedMobileList { get; set; }
}

/// <summary>
/// 企业微信群机器人响应（errcode=0 为成功）.
/// </summary>
public class WeixinWorkRobotResponse
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
