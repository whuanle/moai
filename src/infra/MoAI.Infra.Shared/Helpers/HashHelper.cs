// <copyright file="HashHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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

    // 计算文件 md5
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