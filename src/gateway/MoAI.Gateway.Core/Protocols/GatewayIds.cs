using System.Security.Cryptography;

namespace MoAI.Gateway.Protocols;

/// <summary>
/// 网关响应/调用 id 生成器.
/// </summary>
public static class GatewayIds
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// 生成带前缀的随机 id.
    /// </summary>
    /// <param name="prefix">前缀.</param>
    /// <returns>返回 id.</returns>
    public static string New(string prefix = "")
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var sb = new System.Text.StringBuilder(prefix.Length + 24);
        sb.Append(prefix);
        foreach (var b in bytes)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        return sb.ToString();
    }
}
