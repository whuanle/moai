using System.Security.Cryptography;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 密钥回显信封加密（传输加密）：前端随机生成 AES-256-GCM key，经 RSA(PKCS1) 加密随请求上送（encryptedKey），
/// 服务端解出 key 后以该 key 加密密钥原文返回（iv+data），端到端密文传输、服务端不留存该 key.
/// </summary>
public static class SubsystemSecretEnvelope
{
    /// <summary>
    /// 信封 AES key 字节数（AES-256）.
    /// </summary>
    public const int KeySizeBytes = 32;

    /// <summary>
    /// GCM 随机向量字节数.
    /// </summary>
    public const int IvSizeBytes = 12;

    /// <summary>
    /// 用请求方提供的信封 key 加密原文：返回 iv（base64）与 data（base64，密文+GCM tag）.
    /// </summary>
    /// <param name="envelopeKey">信封对称密钥（32 字节）.</param>
    /// <param name="plainText">明文.</param>
    /// <returns>返回 iv 与 data（均为 base64）.</returns>
    public static (string Iv, string Data) Encrypt(byte[] envelopeKey, string plainText)
    {
        var iv = RandomNumberGenerator.GetBytes(IvSizeBytes);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(envelopeKey, 16);
        gcm.Encrypt(iv, plainBytes, cipherBytes, tag);

        var payload = new byte[cipherBytes.Length + tag.Length];
        cipherBytes.CopyTo(payload, 0);
        tag.CopyTo(payload, cipherBytes.Length);
        return (Convert.ToBase64String(iv), Convert.ToBase64String(payload));
    }
}
