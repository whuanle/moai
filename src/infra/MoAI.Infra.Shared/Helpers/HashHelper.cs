using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Helpers;

/// <summary>
/// 哈希助手类.
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// 计算SHA256哈希值.
    /// </summary>
    /// <param name="text">要哈希的文本.</param>
    /// <returns>哈希值的十六进制字符串表示.</returns>
    public static string ComputeSha256Hash(string text)
    {
        byte[]? bytes = Encoding.UTF8.GetBytes(text);
        byte[]? hash = SHA256.HashData(bytes);

        StringBuilder? builder = new();
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 计算数据的 SHA-256 哈希值.
    /// </summary>
    /// <param name="data">要哈希的数据.</param>
    /// <returns>哈希值的十六进制字符串表示.</returns>
    public static string ComputeSha256(ReadOnlySpan<byte> data)
    {
        byte[] hash = SHA256.HashData(data);
        StringBuilder builder = new(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 计算文件 SHA-256 哈希值.
    /// </summary>
    /// <param name="filePath">文件路径.</param>
    /// <returns>哈希值的十六进制字符串表示.</returns>
    public static string ComputeFileSha256(string filePath)
    {
        using FileStream? stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[]? hash = SHA256.HashData(stream);
        StringBuilder builder = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
