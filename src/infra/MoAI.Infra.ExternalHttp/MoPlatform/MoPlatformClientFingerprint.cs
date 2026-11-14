using System.Security.Cryptography;

namespace MoAI.Infra;

/// <summary>
/// MoAI 安装实例的稳定客户端指纹提供器。指纹只用于平台侧配额归并，不作为密码或身份认证凭据.
/// </summary>
public class MoPlatformClientFingerprint
{
    private static readonly string FingerprintPath = Path.Combine(AppConst.ConfigsPath, "plugins", ".client-fingerprint");
    private readonly Lazy<string> _fingerprint = new(() => LoadOrCreate(FingerprintPath), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 获取当前安装实例的稳定指纹.
    /// </summary>
    public string Fingerprint => _fingerprint.Value;

    /// <summary>
    /// 从指定路径读取或原子创建 256 位随机指纹.
    /// </summary>
    /// <param name="path">持久化路径.</param>
    /// <returns>64 位十六进制指纹.</returns>
    public static string LoadOrCreate(string path)
    {
        if (TryReadFingerprint(path, out var existing))
        {
            return existing;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var temp = path + "." + Guid.CreateVersion7().ToString("N", global::System.Globalization.CultureInfo.InvariantCulture) + ".tmp";
        File.WriteAllText(temp, generated);
        try
        {
            File.Move(temp, path);
            return generated;
        }
        catch (IOException) when (TryReadFingerprint(path, out existing))
        {
            File.Delete(temp);
            return existing;
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static bool TryReadFingerprint(string path, out string fingerprint)
    {
        if (File.Exists(path))
        {
            var value = File.ReadAllText(path).Trim();
            if (value.Length == 64 && value.All(Uri.IsHexDigit))
            {
                fingerprint = value.ToUpperInvariant();
                return true;
            }
        }

        fingerprint = string.Empty;
        return false;
    }
}
