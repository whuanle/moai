namespace MoAI.Subsystem.Commands.Responses;

/// <summary>
/// 重置系统级接入密钥结果：返回展示掩码，原文可随时经 reveal-secret 端点信封加密查看.
/// </summary>
public class ResetSubsysAppSecretCommandResponse
{
    /// <summary>
    /// 新密钥展示掩码（前 4 + **** + 后 4，便于核对）.
    /// </summary>
    public string SecretMask { get; init; } = string.Empty;
}
