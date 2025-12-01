using MoAI.Infra.Helpers;
using MoAI.Infra.Models;

namespace MoAI;

/// <summary>
/// UserIdContextExtensions.
/// </summary>
public static class UserIdContextExtensions
{
    /// <summary>
    /// 设置用户id值.
    /// </summary>
    /// <typeparam name="T">IUserIdContext.</typeparam>
    /// <param name="obj"></param>
    /// <param name="userId"></param>
    public static void SetUserId<T>(this T obj, int userId)
        where T : class, IUserIdContext
    {
        obj.SetProperty(o => o.ContextUserId, userId);
    }
}