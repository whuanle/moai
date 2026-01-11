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
    /// 计算数据的 MD5 哈希值.
    /// </summary>
    /// <param name="data">要哈希的数据.</param>
    /// <returns>哈希值的十六进制字符串表示.</returns>
    public static string ComputeMd5(ReadOnlySpan<byte> data)
    {
        byte[] hash = MD5.HashData(data);
        StringBuilder builder = new(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 计算文件 SHA256 哈希值.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static string ComputeFileMd5(string filePath)
    {
        using FileStream? stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using MD5? md5 = MD5.Create();
        byte[]? hash = md5.ComputeHash(stream);
        StringBuilder builder = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}