using Maomi;
using Microsoft.AspNetCore.Mvc.Filters;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Filters;

/// <summary>
/// 自动赋值 IUserIdContext
/// </summary>
[InjectOnScoped]
public class AutoAssignUserIdFilter : IAsyncActionFilter
{
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoAssignUserIdFilter"/> class.
    /// </summary>
    /// <param name="userContextProvider"></param>
    public AutoAssignUserIdFilter(IUserContextProvider userContextProvider)
    {
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 动作执行前的处理逻辑
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="next">下一个过滤器委托</param>
    /// <returns></returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userContext = _userContextProvider.GetUserContext();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is IUserIdContext userIdContext)
            {
                userIdContext.SetUserId(userContext.UserId);
                userIdContext.SetProperty(a => a.ContextUserType, userContext.UserType);
            }
        }

        await next();
    }
}