using System.Text;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookRequest<T>
{
    /// <summary>
    /// 时间戳，在开启签名时才需要.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    /// <summary>
    /// 签名字符串，在开启签名时才需要.
    /// </summary>
    [JsonPropertyName("sign")]
    public string? Sign { get; set; }

    /// <summary>
    /// 消息类型.
    /// </summary>
    [JsonPropertyName("msg_type")]
    public virtual string MsgType { get; init; } = string.Empty;

    /// <summary>
    /// 消息内容.
    /// </summary>
    [JsonPropertyName("content")]
    public T Content { get; init; } = default!;

    /// <summary>
    /// 计算签名。
    /// </summary>
    /// <param name="signKey">密钥.</param>
    public void BuildSign(string signKey)
    {
        /*
         设置签名校验后，向 webhook 发送请求需要签名校验来保障来源可信。
        所校验的签名需要通过时间戳与秘钥进行算法加密，
        即将timestamp + "\n" + 密钥当做签名字符串，使用 HmacSHA256 算法计算空字符串的签名结果，
        再进行 Base64 编码。其中，timestamp是指距当前时间不超过 1 小时（3600 秒）的时间戳，时间单位：s。例如，1599360473。
         */

        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        this.Timestamp = timestamp;

        // 签名密钥.
        var signSecert = $"{timestamp}\n{signKey}";

        // 计算空字符串.
        var hash = HmacSHA256(signSecert, string.Empty);
        var base64Sign = Convert.ToBase64String(hash);

        this.Sign = base64Sign;
    }

    private static byte[] HmacSHA256(string key, string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return hash;
    }
}
