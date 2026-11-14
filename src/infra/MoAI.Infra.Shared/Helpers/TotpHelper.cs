using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MoAI.Infra.Helpers;

/// <summary>
/// TOTP（RFC 6238）动态口令助手：SHA1 哈希、6 位数字、30 秒步长，兼容 Google Authenticator 等认证器 App.
/// 密钥使用 base32（RFC 4648，无填充）编码存储.
/// </summary>
public static class TotpHelper
{
    private const int StepSeconds = 30;

    private const int DigitCount = 6;

    private const int SecretByteLength = 20;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// 生成随机密钥（base32 编码，无填充）.
    /// </summary>
    /// <returns>base32 密钥字符串.</returns>
    public static string GenerateSecret()
    {
        return Base32Encode(RandomNumberGenerator.GetBytes(SecretByteLength));
    }

    /// <summary>
    /// 构建 otpauth:// URI，认证器 App 扫码添加用.
    /// </summary>
    /// <param name="secret">base32 密钥.</param>
    /// <param name="account">账号标识（一般为用户邮箱）.</param>
    /// <param name="issuer">发行方标识（一般为站点名称）.</param>
    /// <returns>otpauth URI.</returns>
    public static string BuildOtpauthUri(string secret, string account, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(account);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&digits={DigitCount}&period={StepSeconds}&algorithm=SHA1";
    }

    /// <summary>
    /// 计算指定时间步长的动态口令.
    /// </summary>
    /// <param name="base32Secret">base32 密钥.</param>
    /// <param name="timeStep">时间步长序号（Unix 秒 / 步长）.</param>
    /// <returns>6 位数字口令.</returns>
    public static string ComputeCode(string base32Secret, long timeStep)
    {
        byte[] key = Base32Decode(base32Secret);

        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, counter, hash);

        // 动态截断（RFC 4226 5.3）
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        int code = binary % 1_000_000;
        return code.ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 校验动态码，允许 ±window 个步长的时钟偏移；命中时返回对应时间步长（用于防重放）.
    /// </summary>
    /// <param name="base32Secret">base32 密钥.</param>
    /// <param name="code">用户输入的 6 位动态码.</param>
    /// <param name="matchedTimeStep">命中的时间步长，未命中为 0.</param>
    /// <param name="unixTimeSeconds">当前 Unix 时间戳（秒），空取当前时间.</param>
    /// <param name="window">允许偏移的步长数.</param>
    /// <returns>是否校验通过.</returns>
    public static bool TryVerifyCode(string base32Secret, string code, out long matchedTimeStep, long? unixTimeSeconds = null, int window = 1)
    {
        matchedTimeStep = 0;
        if (string.IsNullOrWhiteSpace(base32Secret))
        {
            return false;
        }

        var normalized = code?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length != DigitCount || !normalized.All(char.IsAsciiDigit))
        {
            return false;
        }

        long currentStep = (unixTimeSeconds ?? DateTimeOffset.Now.ToUnixTimeSeconds()) / StepSeconds;
        for (long i = -window; i <= window; i++)
        {
            long candidate = currentStep + i;
            if (candidate < 0)
            {
                continue;
            }

            if (FixedTimeEquals(ComputeCode(base32Secret, candidate), normalized))
            {
                matchedTimeStep = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// base32 编码（RFC 4648，无填充）.
    /// </summary>
    /// <param name="data">原始字节.</param>
    /// <returns>base32 字符串.</returns>
    public static string Base32Encode(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int bitBuffer = 0;
        int bitCount = 0;
        foreach (byte b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                builder.Append(Base32Alphabet[(bitBuffer >> (bitCount - 5)) & 0x1F]);
                bitCount -= 5;
            }
        }

        if (bitCount > 0)
        {
            builder.Append(Base32Alphabet[(bitBuffer << (5 - bitCount)) & 0x1F]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// base32 解码（RFC 4648，容忍小写、空白与填充符）.
    /// </summary>
    /// <param name="base32">base32 字符串.</param>
    /// <returns>原始字节.</returns>
    public static byte[] Base32Decode(string base32)
    {
        var trimmed = new string(base32.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        int bitBuffer = 0;
        int bitCount = 0;
        var output = new List<byte>(trimmed.Length * 5 / 8);
        foreach (char c in trimmed)
        {
            int value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"无效的 base32 字符: {c}");
            }

            bitBuffer = (bitBuffer << 5) | value;
            bitCount += 5;
            if (bitCount >= 8)
            {
                output.Add((byte)((bitBuffer >> (bitCount - 8)) & 0xFF));
                bitCount -= 8;
            }
        }

        return output.ToArray();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    }
}
