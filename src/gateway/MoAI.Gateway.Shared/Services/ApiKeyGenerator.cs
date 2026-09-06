using System.Security.Cryptography;
using System.Text;

namespace MoAI.Gateway.Services;

/// <summary>
/// 网关 API Key 生成器，密钥原文只在创建时返回一次，服务端仅保存 sha256.
/// </summary>
public static class ApiKeyGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// 密钥展示前缀长度：moai- + 8 位随机字符.
    /// </summary>
    public const int PrefixLength = 13;

    /// <summary>
    /// 生成新密钥.
    /// </summary>
    /// <returns>返回密钥原文、展示前缀与 sha256（小写 hex）.</returns>
    public static (string Secret, string KeyPrefix, string Sha256Hex) New()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var sb = new StringBuilder(37);
        sb.Append("moai-");
        foreach (var b in bytes)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        var secret = sb.ToString();
        return (secret, secret[..PrefixLength], Hash(secret));
    }

    /// <summary>
    /// 计算密钥的 sha256（小写 hex），与 DatabaseContext 中 Sha256 hex→bytea 转换器约定一致.
    /// </summary>
    /// <param name="secret">密钥原文.</param>
    /// <returns>返回 sha256 hex 字符串.</returns>
    public static string Hash(string secret)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
    }
}
