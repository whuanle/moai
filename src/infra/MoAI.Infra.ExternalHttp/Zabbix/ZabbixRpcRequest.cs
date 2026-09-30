using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Zabbix;

/// <summary>
/// Zabbix JSON-RPC 2.0 请求报文（<c>api_jsonrpc.php</c>）.
/// </summary>
/// <remarks>
/// <c>params</c> 形态随方法而变（对象字段由调用方拼装），统一用字典承载，
/// 字典值为运行时类型（字符串/数字/布尔/数组）原样透出.
/// </remarks>
public class ZabbixRpcRequest
{
    /// <summary>
    /// 协议版本，固定 2.0.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    /// <summary>
    /// API 方法名，如 <c>problem.get</c>、<c>apiinfo.version</c>.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 方法参数.
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, object?> Params { get; set; } = new();

    /// <summary>
    /// 会话或 API 令牌（body auth 形态）；走 Authorization 头形态时置 null.
    /// </summary>
    [JsonPropertyName("auth")]
    public string? Auth { get; set; }

    /// <summary>
    /// 请求 ID，固定 1（单请求单响应）.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;
}
