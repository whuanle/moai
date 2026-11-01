using MoAI.Infra.Helpers;

namespace MoAI.Auth.Services;

/// <summary>
/// TOTP（两步验证）动态口令服务实现：<see cref="TotpHelper"/>（RFC 6238，SHA1/6 位/30 秒步长）的薄封装.
/// </summary>
public class TotpService : ITotpService
{
    /// <inheritdoc/>
    public string GenerateSecret() => TotpHelper.GenerateSecret();

    /// <inheritdoc/>
    public string BuildOtpauthUri(string secret, string account, string issuer) => TotpHelper.BuildOtpauthUri(secret, account, issuer);

    /// <inheritdoc/>
    public bool TryVerifyCode(string base32Secret, string code, out long matchedTimeStep) => TotpHelper.TryVerifyCode(base32Secret, code, out matchedTimeStep);
}
