using System.Security.Cryptography;
using System.Text;
using Maomi;
using MoAI.Infra.Helpers;
using MoAI.Infra.Service;

namespace MoAI.Subsystem.Services;

/// <summary>
/// <inheritdoc cref="ISubsystemSecretCipher"/>
/// </summary>
[InjectOnScoped]
public class SubsystemSecretCipher : ISubsystemSecretCipher
{
    /// <summary>
    /// 可回显存储格式前缀（v2: + AES 密文）；无前缀视为存量 PBKDF2 哈希.
    /// </summary>
    public const string ReversiblePrefix = "v2:";

    private readonly IAESProvider _aesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSecretCipher"/> class.
    /// </summary>
    /// <param name="aesProvider">AES 加解密服务（key 取 SystemOptions.AES）.</param>
    public SubsystemSecretCipher(IAESProvider aesProvider)
    {
        _aesProvider = aesProvider;
    }

    /// <inheritdoc/>
    public string NewSecret() => SubsystemSecretGenerator.New();

    /// <inheritdoc/>
    public string Mask(string secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < 12)
        {
            return "****";
        }

        return $"{secret[..4]}****{secret[^4..]}";
    }

    /// <inheritdoc/>
    public string EncryptForStorage(string secret)
    {
        return ReversiblePrefix + _aesProvider.Encrypt(secret);
    }

    /// <inheritdoc/>
    public bool Verify(string stored, string salt, string input)
    {
        if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(input))
        {
            return false;
        }

        if (stored.StartsWith(ReversiblePrefix, StringComparison.Ordinal))
        {
            if (!TryReveal(stored, out var secret))
            {
                return false;
            }

            // 常量时间比较，避免时序侧信道
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(input));
        }

        if (string.IsNullOrEmpty(salt))
        {
            return false;
        }

        return PBKDF2Helper.VerifyHash(input, stored, salt);
    }

    /// <inheritdoc/>
    public bool TryReveal(string stored, out string secret)
    {
        secret = string.Empty;
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(ReversiblePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            secret = _aesProvider.Decrypt(stored[ReversiblePrefix.Length..]);
            return !string.IsNullOrEmpty(secret);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
