using Maomi;
using Microsoft.AspNetCore.Mvc.Filters;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Filters;

/// <summary>
/// 自动赋值 IUserIdContext
/// </summary>
[InjectOnScoped]
public class AutoAssignUserIdFilter : IAsyncActionFilter
{
    private readonly IUserContextProvider _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoAssignUserIdFilter"/> class.
    /// </summary>
    /// <param name="userContext"></param>
    public AutoAssignUserIdFilter(IUserContextProvider userContext)
    {
        _userContext = userContext;
    }

    /// <summary>
    /// 动作执行前的处理逻辑
    /// </summary>
    /// <param name="context">动作执行上下文</param>
    /// <param name="next">下一个过滤器委托</param>
    /// <returns></returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userContext = _userContext.GetUserContext();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is IUserIdContext userIdContext)
            {
                userIdContext.SetUserId(userContext.UserId);
            }
        }

        await next();
    }
}