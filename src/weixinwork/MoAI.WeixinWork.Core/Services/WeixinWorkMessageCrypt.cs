using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信回调消息加解密（与公众平台/企业微信「加解密方案」一致）：
/// 签名为 sha1(sort(token, timestamp, nonce, encrypt))；AES-256-CBC，密钥为 EncodingAESKey 补一个
/// 等号后 Base64 解码的 32 字节，IV 取密钥前 16 字节；明文结构为 随机串(16B) + 消息长度(4B 大端) + 消息 + receiveid.
/// </summary>
public static class WeixinWorkMessageCrypt
{
    /// <summary>
    /// 校验回调签名（msg_signature）.
    /// </summary>
    /// <param name="token">回调配置 Token.</param>
    /// <param name="timestamp">时间戳.</param>
    /// <param name="nonce">随机数.</param>
    /// <param name="encrypt">加密消息体（验证 URL 时为加密 echostr）.</param>
    /// <param name="signature">待校验的 msg_signature.</param>
    /// <returns>返回签名是否有效.</returns>
    public static bool VerifySignature(string token, string timestamp, string nonce, string encrypt, string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var raw = string.Concat(new[] { token, timestamp, nonce, encrypt }.OrderBy(x => x, StringComparer.Ordinal));
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(raw)));
        return string.Equals(hash, signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解密回调消息；同时校验明文尾部的 receiveid 与连接配置的企业 ID 一致（防跨企业串扰）.
    /// </summary>
    /// <param name="encodingAesKey">回调配置 EncodingAESKey（43 字符）.</param>
    /// <param name="encryptedBase64">加密消息体.</param>
    /// <param name="expectedReceiveId">期望的企业 ID，空串跳过校验.</param>
    /// <param name="message">解密出的明文报文.</param>
    /// <param name="receiveId">解密出的 receiveid（企业 ID）.</param>
    /// <param name="error">失败原因.</param>
    /// <returns>返回是否解密成功.</returns>
    public static bool TryDecrypt(
        string encodingAesKey,
        string encryptedBase64,
        string expectedReceiveId,
        out string message,
        out string receiveId,
        out string error)
    {
        message = string.Empty;
        receiveId = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(encodingAesKey) || encodingAesKey.Length != 43)
        {
            error = "EncodingAESKey 须为 43 位字符串.";
            return false;
        }

        byte[] aesKey;
        try
        {
            aesKey = Convert.FromBase64String(encodingAesKey + "=");
        }
        catch (FormatException)
        {
            error = "EncodingAESKey 不是合法的 Base64 串.";
            return false;
        }

        if (aesKey.Length != 32)
        {
            error = "EncodingAESKey 解码后须为 32 字节.";
            return false;
        }

        byte[] cipher;
        try
        {
            cipher = Convert.FromBase64String(encryptedBase64);
        }
        catch (FormatException)
        {
            error = "加密消息体不是合法的 Base64 串.";
            return false;
        }

        if (cipher.Length == 0 || cipher.Length % 16 != 0)
        {
            error = "加密消息体长度非法.";
            return false;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.IV = aesKey.AsSpan(0, 16).ToArray();

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            if (plain.Length < 20)
            {
                error = "解密报文长度非法.";
                return false;
            }

            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(plain.AsSpan(16, 4));
            if (length < 0 || 20 + length > plain.Length)
            {
                error = "解密报文长度非法.";
                return false;
            }

            message = Encoding.UTF8.GetString(plain, 20, length);
            receiveId = Encoding.UTF8.GetString(plain, 20 + length, plain.Length - 20 - length);

            if (!string.IsNullOrEmpty(expectedReceiveId) && !string.Equals(receiveId, expectedReceiveId, StringComparison.Ordinal))
            {
                error = "报文归属企业与连接配置不一致.";
                return false;
            }

            return true;
        }
        catch (CryptographicException ex)
        {
            error = $"解密失败：{ex.Message}";
            return false;
        }
    }
}
