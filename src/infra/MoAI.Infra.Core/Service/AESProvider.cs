// <copyright file="AESProvider.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Service;
public class AESProvider : IAESProvider
{
    private readonly string _key;
    private readonly byte[] _keyBytes;

    public AESProvider(string key)
    {
        _key = key;
        _keyBytes = Encoding.UTF8.GetBytes(_key.PadRight(32).Substring(0, 32));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentNullException(nameof(plainText));
        }

        using (Aes? aesAlg = Aes.Create())
        {
            aesAlg.Key = _keyBytes;
            aesAlg.GenerateIV();
            byte[]? iv = aesAlg.IV;

            ICryptoTransform? encryptor = aesAlg.CreateEncryptor();

            using (MemoryStream? msEncrypt = new())
            {
                // 先写入 IV
                msEncrypt.Write(iv, 0, iv.Length);

                using (CryptoStream? csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (StreamWriter? swEncrypt = new(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }

                return Convert.ToBase64String(msEncrypt.ToArray());
            }
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            throw new ArgumentNullException(nameof(cipherText));
        }

        byte[] fullCipherBytes = Convert.FromBase64String(cipherText);

        using (Aes? aesAlg = Aes.Create())
        {
            aesAlg.Key = _keyBytes;

            // 从密文中提取 IV（前16字节）
            byte[] iv = new byte[16];
            byte[] actualCipherText = new byte[fullCipherBytes.Length - 16];

            Buffer.BlockCopy(fullCipherBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(fullCipherBytes, 16, actualCipherText, 0, actualCipherText.Length);

            aesAlg.IV = iv;

            ICryptoTransform? decryptor = aesAlg.CreateDecryptor();

            using (MemoryStream? msDecrypt = new(actualCipherText))
            using (CryptoStream? csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read))
            using (StreamReader? srDecrypt = new(csDecrypt))
            {
                return srDecrypt.ReadToEnd();
            }
        }
    }
}