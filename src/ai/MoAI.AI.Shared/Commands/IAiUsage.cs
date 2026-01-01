using MoAI.Infra.Models;

namespace MoAI.AI.Commands;

/// <summary>
/// 方便记录统计信息.
/// </summary>
public interface IAiUsage : IUserIdContext
{
    /// <summary>
    /// 方便记录统计信息。
    /// </summary>
    public int AiModelId { get; }

    /// <summary>
    /// 渠道.
    /// </summary>
    public string Channel { get; }
}