using System.Security.Cryptography;
using System.Text;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 系统应用接入 key 生成器，key 原文只在创建时返回一次（列内明文存储，作为协作面 Bearer 凭证）.
/// </summary>
public static class SystemAccessAppKeyGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// key 展示前缀长度：moai-sa- + 4 位随机字符.
    /// </summary>
    public const int PrefixLength = 12;

    /// <summary>
    /// 生成新 key.
    /// </summary>
    /// <returns>返回 key 原文与展示前缀.</returns>
    public static (string Key, string KeyPrefix) New()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var sb = new StringBuilder(40);
        sb.Append("moai-sa-");
        foreach (var b in bytes)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        var key = sb.ToString();
        return (key, key[..PrefixLength]);
    }
}
