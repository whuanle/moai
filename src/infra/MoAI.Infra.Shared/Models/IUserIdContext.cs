namespace MoAI.Infra.Models;

/// <summary>
/// 用户 id 上下文，在 ASP.NET Core 请求管道中被自动赋值.
/// </summary>
public interface IUserIdContext
{
    /// <summary>
    /// 通过上下文自动配置id，前端不需要传递.
    /// </summary>
    public int ContextUserId { get; init; }
}
