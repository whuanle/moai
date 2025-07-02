// <copyright file="RsaProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.IdentityModel.Tokens;
using MoAI.Infra.Services;
using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Service;

/// <summary>
/// RSA 处理.
/// </summary>
public class RsaProvider : IRsaProvider
{
    private readonly RSA _rsaPrivate;
    private readonly RsaSecurityKey _rsaSecurityKey;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaProvider"/> class.
    /// </summary>
    /// <param name="rsaPem"></param>
    public RsaProvider(string rsaPem)
    {
        _rsaPrivate = RSA.Create();
        _rsaPrivate.ImportFromPem(rsaPem);
        _rsaSecurityKey = new RsaSecurityKey(_rsaPrivate);
    }

    /// <inheritdoc/>
    public string GetPublicKey()
    {
        return Convert.ToBase64String(_rsaPrivate.ExportSubjectPublicKeyInfo());
    }

    /// <inheritdoc/>
    public string GetPublicKeyPem()
    {
        return _rsaPrivate.ExportRSAPublicKeyPem();
    }

    /// <inheritdoc/>
    public string Encrypt(string message, RSAEncryptionPadding? padding = null)
    {
        if (padding == null)
        {
            padding = RSAEncryptionPadding.Pkcs1;
        }

        byte[]? encryptData = _rsaPrivate.Encrypt(Encoding.UTF8.GetBytes(message), padding);
        return Convert.ToBase64String(encryptData);
    }

    /// <inheritdoc/>
    public string Decrypt(string message, RSAEncryptionPadding? padding = null)
    {
        if (padding == null)
        {
            padding = RSAEncryptionPadding.Pkcs1;
        }

        byte[]? cipherByteData = Convert.FromBase64String(message);

        byte[]? encryptData = _rsaPrivate.Decrypt(cipherByteData, padding);
        return Encoding.UTF8.GetString(encryptData);
    }

    /// <inheritdoc/>
    public RSA GetPrivateRsa()
    {
        return _rsaPrivate;
    }

    /// <inheritdoc/>
    public RsaSecurityKey GetRsaSecurityKey()
    {
        return _rsaSecurityKey;
    }
}