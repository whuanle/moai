using System.Text.Json.Serialization;

namespace MoAI.Message.Models;

/// <summary>
/// SignalR 事件推送负载：只推事件不推业务数据，data 仅允许标识类字段（id、类型名等），
/// 禁止携带业务正文，消费方收到后调用对应 REST 接口取详情.
/// </summary>
public class EventPushItem
{
    /// <summary>
    /// 事件名，见 <see cref="EventNames"/>.
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// 标识类扩展字段（可选），如 { "source": "publication" }，禁止放业务正文.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, string>? Data { get; set; }

    /// <summary>
    /// 事件产生时间.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; set; }
}
