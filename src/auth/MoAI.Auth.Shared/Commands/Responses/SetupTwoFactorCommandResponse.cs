namespace MoAI.Auth.Commands.Responses;

/// <summary>
/// 两步验证（2FA）绑定密钥.
/// </summary>
public class SetupTwoFactorCommandResponse
{
    /// <summary>
    /// base32 密钥（认证器 App 手动输入用）.
    /// </summary>
    public string Secret { get; set; } = default!;

    /// <summary>
    /// otpauth:// URI（前端渲染二维码用）.
    /// </summary>
    public string OtpauthUri { get; set; } = default!;
}
