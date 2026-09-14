using System.Security.Cryptography;
using System.Text;

namespace MoAI.App.Services;

/// <summary>
/// 应用接入 key 生成器，密钥原文只在创建时返回一次（列内明文存储）.
/// </summary>
public static class AccessAppKeyGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// 密钥展示前缀长度：moai-ac- + 4 位随机字符.
    /// </summary>
    public const int PrefixLength = 12;

    /// <summary>
    /// 生成新密钥.
    /// </summary>
    /// <returns>返回密钥原文与展示前缀.</returns>
    public static (string Secret, string KeyPrefix) New()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var sb = new StringBuilder(40);
        sb.Append("moai-ac-");
        foreach (var b in bytes)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        var secret = sb.ToString();
        return (secret, secret[..PrefixLength]);
    }
}
