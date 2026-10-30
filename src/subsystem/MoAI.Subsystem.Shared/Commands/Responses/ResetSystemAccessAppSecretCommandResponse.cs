namespace MoAI.Subsystem.Commands.Responses;

/// <summary>
/// 重置系统应用接入密钥结果：新密钥原文一次性返回（此后仅可凭前缀识别）.
/// </summary>
public class ResetSystemAccessAppSecretCommandResponse
{
    /// <summary>
    /// 新接入密钥原文（仅此一次返回，请妥善保存）.
    /// </summary>
    public string Secret { get; init; } = default!;
}
