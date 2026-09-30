using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.DingTalk;

/// <summary>
/// 钉钉自定义机器人「加签」计算：sign = base64(HMAC-SHA256(secret, "{timestampMs}\n{secret}"))，再 URL 编码.
/// </summary>
public static class DingTalkSignHelper
{
    /// <summary>
    /// 计算加签参数.
    /// </summary>
    /// <param name="secret">加签密钥.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>时间戳（毫秒）与 URL 编码后的 sign.</returns>
    public static (string Timestamp, string Sign) Build(string secret, DateTimeOffset now)
    {
        var timestamp = now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}\n{secret}"));
        var sign = Uri.EscapeDataString(Convert.ToBase64String(hash));
        return (timestamp, sign);
    }
}
