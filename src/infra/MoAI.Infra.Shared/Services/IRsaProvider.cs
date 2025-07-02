// <copyright file="IRsaProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace MoAI.Infra.Services;

/// <summary>
/// RSA 服务.
/// </summary>
public interface IRsaProvider
{
    /// <summary>
    /// 解密.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="padding"></param>
    /// <returns>解密后的字符串.</returns>
    string Decrypt(string message, RSAEncryptionPadding? padding = null);

    /// <summary>
    /// 加密.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="padding"></param>
    /// <returns></returns>
    string Encrypt(string message, RSAEncryptionPadding? padding = null);

    /// <summary>
    /// 导出 pem 格式的公钥.
    /// </summary>
    /// <returns></returns>
    string GetPublicKeyPem();

    /// <summary>
    /// 导出公钥.
    /// </summary>
    /// <returns></returns>
    string GetPublicKey();

    /// <summary>
    /// 获取 RSA 对象.
    /// </summary>
    /// <returns></returns>
    RSA GetPrivateRsa();

    /// <summary>
    /// RsaSecurityKey.
    /// </summary>
    /// <returns></returns>
    RsaSecurityKey GetRsaSecurityKey();
}