
#pragma warning disable CA1031 // 不捕获常规异常类型
using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Helpers;

/// <summary>
/// 密码服务 - 提供密码哈希和验证功能
/// </summary>
public static class PBKDF2Helper
{
    private const int PBKDF2Itreation = 10000; // 迭代次数
    private const int SaltLength = 32;
    private const int OutputLength = 32;

    /// <summary>
    /// 计算字符串 PBKDF2 哈希值.
    /// </summary>
    /// <param name="sourceText">原文.</param>
    /// <returns>返回 base64 的哈希文和解码的 salt.</returns>
    public static (string HashBase64Text, string SaltBase64) ToHash(string sourceText)
    {
        byte[] salt = new byte[SaltLength];
        using (RandomNumberGenerator? rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(sourceText),
            salt,
            PBKDF2Itreation,
            HashAlgorithmName.SHA256,
            OutputLength);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// 计算字符串 PBKDF2 哈希值.
    /// </summary>
    /// <param name="sourceText">原文.</param>
    /// <param name="saltBase64"></param>
    /// <returns>返回 base64 的哈希文.</returns>
    public static string ToHash(string sourceText, string saltBase64)
    {
        byte[] salt = Convert.FromBase64String(saltBase64);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(sourceText),
            salt,
            PBKDF2Itreation,
            HashAlgorithmName.SHA256,
            OutputLength);

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 检查源字符串和哈希字符串是否一致.
    /// </summary>
    /// <param name="sourceText"></param>
    /// <param name="hashBase64Text"></param>
    /// <param name="saltBase64"></param>
    /// <returns>是否一致.</returns>
    public static bool VerifyHash(string sourceText, string hashBase64Text, string saltBase64)
    {
        try
        {
            byte[] saltBytes = Convert.FromBase64String(saltBase64);
            byte[] hashBytes = Convert.FromBase64String(hashBase64Text);

            var sourceHashBase64Text = ToHash(sourceText, saltBase64);
            return string.CompareOrdinal(sourceHashBase64Text, hashBase64Text) == 0;
        }
        catch
        {
            return false;
        }
    }
}
