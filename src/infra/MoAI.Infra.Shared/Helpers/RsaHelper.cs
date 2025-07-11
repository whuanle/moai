// <copyright file="RsaHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Helpers;

/// <summary>
/// Rsa 加解密.
/// </summary>
public static class RsaHelper
{
    /// <summary>
    /// 导出 pck 8 公钥.
    /// </summary>
    /// <param name="rsa"></param>
    /// <returns></returns>
    public static string ExportPublichKeyPck8(this RSA rsa)
    {
        StringBuilder builder = new();
        builder.AppendLine("-----BEGIN PUBLIC KEY-----");
        builder.AppendLine(Convert.ToBase64String(
            rsa.ExportSubjectPublicKeyInfo(),
            Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END PUBLIC KEY-----");
        return builder.ToString();
    }

    /// <summary>
    /// 导入公钥.
    /// </summary>
    /// <param name="rsa"></param>
    /// <param name="publicKey"></param>
    public static void ImportPublichKeyPck8(this RSA rsa, string publicKey)
    {
        publicKey = publicKey
            .Replace("-----BEGIN RSA PUBLIC KEY-----", string.Empty, StringComparison.CurrentCultureIgnoreCase)
            .Replace("-----END RSA PUBLIC KEY-----", string.Empty, StringComparison.CurrentCultureIgnoreCase)
            .Replace("-----BEGIN PUBLIC KEY-----", string.Empty, StringComparison.CurrentCultureIgnoreCase)
            .Replace("-----END PUBLIC KEY-----", string.Empty, StringComparison.CurrentCultureIgnoreCase)
            .Replace("\r", string.Empty, StringComparison.CurrentCultureIgnoreCase)
            .Replace("\n", string.Empty, StringComparison.CurrentCultureIgnoreCase);

        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out int bytesRead);
    }

    /// <summary>
    /// 加密信息.
    /// </summary>
    /// <param name="rsa"></param>
    /// <param name="message"></param>
    /// <param name="padding">如 <see cref="RSAEncryptionPadding.OaepSHA256"/></param>
    /// <returns></returns>
    public static string Encrypt(this RSA rsa, string message, RSAEncryptionPadding padding)
    {
        byte[]? encryptData = rsa.Encrypt(Encoding.UTF8.GetBytes(message), padding);
        return Convert.ToBase64String(encryptData);
    }

    /// <summary>
    /// 解密
    /// </summary>
    /// <param name="rsa"></param>
    /// <param name="message"></param>
    /// <param name="padding"></param>
    /// <returns></returns>
    public static string Decrypt(this RSA rsa, string message, RSAEncryptionPadding padding)
    {
        byte[]? cipherByteData = Convert.FromBase64String(message);

        byte[]? encryptData = rsa.Decrypt(cipherByteData, padding);
        return Encoding.UTF8.GetString(encryptData);
    }
}