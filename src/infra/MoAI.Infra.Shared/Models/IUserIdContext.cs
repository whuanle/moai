namespace MoAI.Infra.Models;

/// <summary>
/// 用户 id 上下文.
/// </summary>
public interface IUserIdContext
{
    /// <summary>
    /// 通过上下文自动配置id.
    /// </summary>
    public int ContextUserId { get; init; }
}
