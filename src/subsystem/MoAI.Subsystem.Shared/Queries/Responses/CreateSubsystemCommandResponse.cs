namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 创建子系统结果：接入密钥原文仅此一次返回，不再可查.
/// </summary>
public class CreateSubsystemCommandResponse
{
    /// <summary>
    /// 子系统 id.
    /// </summary>
    public Guid SubsystemId { get; init; }

    /// <summary>
    /// 接入密钥原文（仅此一次）.
    /// </summary>
    public string Secret { get; init; } = default!;
}
