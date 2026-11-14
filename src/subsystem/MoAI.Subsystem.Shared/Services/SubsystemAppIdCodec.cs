using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Maomi;
using MoAI.Infra;
using MoAI.Infra.Service;

namespace MoAI.Subsystem.Services;

/// <summary>
/// <inheritdoc cref="ISubsystemAppIdCodec"/>
/// </summary>
[InjectOnScoped]
public class SubsystemAppIdCodec : ISubsystemAppIdCodec
{
    /// <summary>
    /// appid 有效总长 = 前缀 8 + Base64Url(20B)=27，共 35 字符.
    /// </summary>
    public const int EncodedLength = 35;

    private const int GuidBytes = 16;

    private const int MacBytes = 4;

    private const int PayloadBytes = GuidBytes + MacBytes;

    private const string HmacDomain = "moai:appid-hmac:";

    private readonly byte[] _hmacKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemAppIdCodec"/> class.
    /// </summary>
    /// <param name="systemOptions">系统配置（AES 派生 HMAC key；更换 AES 会使全部结构化 appid 失效，与密钥可逆存储同风险口径）.</param>
    public SubsystemAppIdCodec(SystemOptions systemOptions)
    {
        _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(HmacDomain + systemOptions.AES));
    }

    /// <inheritdoc/>
    public string New(Guid id)
    {
        var guidBytes = id.ToByteArray();
        using var hmac = new HMACSHA256(_hmacKey);
        var mac = hmac.ComputeHash(guidBytes);
        Span<byte> payload = stackalloc byte[PayloadBytes];
        guidBytes.CopyTo(payload);
        mac[..MacBytes].CopyTo(payload[GuidBytes..]);
        return SubsystemAuthDefaults.KeyPrefixSystemApp + Base64Url.EncodeToString(payload);
    }

    /// <inheritdoc/>
    public bool TryDecode(string key, out Guid id)
    {
        id = Guid.Empty;
        if (key.Length != EncodedLength || !key.StartsWith(SubsystemAuthDefaults.KeyPrefixSystemApp, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] payload;
        try
        {
            payload = Base64Url.DecodeFromChars(key[SubsystemAuthDefaults.KeyPrefixSystemApp.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (payload.Length != PayloadBytes)
        {
            return false;
        }

        using var hmac = new HMACSHA256(_hmacKey);
        var expected = hmac.ComputeHash(payload[..GuidBytes]);
        if (!CryptographicOperations.FixedTimeEquals(expected.AsSpan(0, MacBytes), payload.AsSpan(GuidBytes, MacBytes)))
        {
            return false;
        }

        id = new Guid(payload[..GuidBytes]);
        return true;
    }
}
