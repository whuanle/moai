using System;
using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Helpers;

/// <summary>
/// 安全哈希加盐文本派生：从系统 AES 密钥与数据库连接串做域分离派生，
/// 部署内确定（重启/多实例一致，支撑 hash 策略的可关联性），换部署即换盐（旧哈希不再可关联）.
/// </summary>
public static class SecurityHashPepperHelper
{
    /// <summary>
    /// 派生加盐文本（hex 小写）.
    /// </summary>
    /// <param name="aes">系统 AES 密钥（system.json System:AES）.</param>
    /// <param name="database">数据库连接字符串.</param>
    /// <returns>加盐文本.</returns>
    public static string Derive(string aes, string database)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"moai:security-hash-pepper:{aes}:{database}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
