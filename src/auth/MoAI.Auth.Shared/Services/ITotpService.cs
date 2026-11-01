namespace MoAI.Auth.Services;

/// <summary>
/// TOTP（两步验证）动态口令服务.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// 生成随机 base32 密钥.
    /// </summary>
    /// <returns>base32 密钥字符串.</returns>
    string GenerateSecret();

    /// <summary>
    /// 构建 otpauth:// URI，认证器 App 扫码添加用.
    /// </summary>
    /// <param name="secret">base32 密钥.</param>
    /// <param name="account">账号标识（一般为用户邮箱）.</param>
    /// <param name="issuer">发行方标识（一般为站点名称）.</param>
    /// <returns>otpauth URI.</returns>
    string BuildOtpauthUri(string secret, string account, string issuer);

    /// <summary>
    /// 校验动态码，允许 ±1 个步长（30 秒）时钟偏移；命中时返回对应时间步长（用于防重放）.
    /// </summary>
    /// <param name="base32Secret">base32 密钥.</param>
    /// <param name="code">用户输入的 6 位动态码.</param>
    /// <param name="matchedTimeStep">命中的时间步长，未命中为 0.</param>
    /// <returns>是否校验通过.</returns>
    bool TryVerifyCode(string base32Secret, string code, out long matchedTimeStep);
}
