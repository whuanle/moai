using System.Security.Cryptography;
using System.Text;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 子系统接入密钥生成器：密钥原文仅在创建/重置时返回一次，库内只存 PBKDF2 哈希.
/// </summary>
public static class SubsystemSecretGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// 生成新密钥（48 位随机字符，无前缀）.
    /// </summary>
    /// <returns>返回密钥原文.</returns>
    public static string New()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var sb = new StringBuilder(48);
        foreach (var b in bytes)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        return sb.ToString();
    }
}
