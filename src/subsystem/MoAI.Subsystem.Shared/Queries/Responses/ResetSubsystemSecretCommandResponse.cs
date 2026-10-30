namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 重置子系统接入密钥结果：新密钥原文仅此一次返回.
/// </summary>
public class ResetSubsystemSecretCommandResponse
{
    /// <summary>
    /// 接入密钥原文（仅此一次）.
    /// </summary>
    public string Secret { get; init; } = default!;
}
