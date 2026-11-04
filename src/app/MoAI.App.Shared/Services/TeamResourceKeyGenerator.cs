using System.Security.Cryptography;
using System.Text;

namespace MoAI.App.Services;

/// <summary>
/// 团队资源 key 生成器（moai-tr- 前缀）：应用 ACP/A2A、知识库、知识图谱的外部访问凭证，
/// key 原文只在生成/重置时返回一次（列内明文存储，等值命中）.
/// </summary>
public static class TeamResourceKeyGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// key 展示前缀长度：moai-tr- + 4 位随机字符.
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
        sb.Append(ExternalAuthDefaults.ResourceKeyPrefix);
        foreach (var b in bytes)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        var key = sb.ToString();
        return (key, key[..PrefixLength]);
    }
}
