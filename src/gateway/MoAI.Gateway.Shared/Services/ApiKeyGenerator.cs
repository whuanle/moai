using System.Security.Cryptography;
using System.Text;

namespace MoAI.Gateway.Services;

/// <summary>
/// 接入 key 摘要工具：应用接入 key 的缓存键与失效钩子共用 sha256（小写 hex）.
/// </summary>
public static class ApiKeyGenerator
{
    /// <summary>
    /// 计算密钥的 sha256（小写 hex）.
    /// </summary>
    /// <param name="secret">密钥原文.</param>
    /// <returns>返回 sha256 hex 字符串.</returns>
    public static string Hash(string secret)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
    }
}
