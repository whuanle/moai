using MoAI.Infra.Models;

namespace MoAI.Infra.Services;

/// <summary>
/// 用户上下文提供者.
/// </summary>
public interface IUserContextProvider
{
    /// <summary>
    /// 获取用户上下文.
    /// </summary>
    /// <returns><see cref="UserContext"/>.</returns>
    UserContext GetUserContext();
}
